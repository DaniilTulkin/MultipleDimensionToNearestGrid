using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MultipleDimensionToNearestGrid
{
    public class MainWindowModelService
    {
        private UIApplication app;
        private UIDocument uidoc;
        private Document doc;
        private RevitEvent revitEvent;
        private Options geometryOptions;

        public MainWindowModelService(UIApplication app)
        {
            this.app = app;
            uidoc = app.ActiveUIDocument;
            doc = uidoc.Document;
            revitEvent = new RevitEvent();

            geometryOptions = new Options
            {
                View = doc.ActiveView,
                IncludeNonVisibleObjects = true,
                ComputeReferences = true
            };
        }
        
        internal void GetFamilyTypesOnCurrentView(ObservableCollection<Element> familyTypes)
        {
            var customFamilies = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(f => f.Symbol.Family)
                .GroupBy(f => f.Name)
                .Select(g => g.First());

            var systemFamilies = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(HostObject))
                .Cast<HostObject>()
                .Select(f => doc.GetElement(f.GetTypeId()))
                .GroupBy(f => f.Name)
                .Select(g => g.First());

            var list = customFamilies.Union(systemFamilies).OrderBy(x => x.Name);
            foreach (var item in list) familyTypes.Add(item);
        }

        internal void MultipleDimentionToNearestGrid(Element selectedFamily, int multiple, bool createDimension)
        {
            if (!ProperView.PermitedView(doc.ActiveView)) return;

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Grid))
                .WhereElementIsNotElementType()
                .Cast<Grid>()
                .ToList();
            if (!grids.Any()) return;

            using (Transaction t = new Transaction(doc, "Кратное расстояние"))
            {
                t.Start();

                List<Element> elements = new List<Element>();
                if (selectedFamily is Family)
                {
                    foreach (FamilySymbol familySymbol in new FilteredElementCollector(doc).WherePasses(new FamilySymbolFilter(selectedFamily.Id)))
                    {
                        foreach (FamilyInstance familyInstance in new FilteredElementCollector(doc, doc.ActiveView.Id).WherePasses(new FamilyInstanceFilter(doc, familySymbol.Id)))
                        {
                            elements.Add(familyInstance);
                        }
                    }                    
                }
                else if (selectedFamily is HostObjAttributes)
                {
                    foreach (Element systemInstanse in new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .Where(f => doc.GetElement(f.GetTypeId()) is HostObjAttributes)
                        .Where(f => doc.GetElement(f.GetTypeId()).Name == selectedFamily.Name))
                    {
                        elements.Add(systemInstanse);
                    }
                }

                foreach (Element element in elements)
                {
                    List<Line> geometryLines = GetSymbolGeometryLines(element);
                    if (geometryLines == null) continue;

                    List<InformationAboutLineModel> InformationAboutLineModelList = GetInformationAboutLines(element, geometryLines, grids);
                    if (InformationAboutLineModelList == null) continue;

                    int i = 0;
                    XYZ point1 = null;
                    foreach (InformationAboutLineModel informationAboutLineModel in InformationAboutLineModelList)
                    {
                        if (i != 2)
                        {
                            Line line = informationAboutLineModel.CurrentLine;
                            Grid grid = informationAboutLineModel.NearestGridToLine;
                            double distance = informationAboutLineModel.DistanceBetweenLineAndGrid;

                            if (i != 0)
                            {
                                Line gridLine1 = InformationAboutLineModelList[i - 1].NearestGridToLine.Curve as Line;
                                Line gridLine2 = grid.Curve as Line;
                                if (IsEqual(gridLine1, gridLine2)) continue;
                            }

                            XYZ point = XYZ.Zero;
                            if (element is FamilyInstance)
                            {
                                LocationPoint locationPoint = element.Location as LocationPoint;
                                point = locationPoint.Point;
                            }
                            try
                            {
                                point1 = line.GetEndPoint(0) + point;
                            }
                            catch (Autodesk.Revit.Exceptions.InternalException)
                            {

                            }
                            XYZ point2 = grid.Curve.Project(point1).XYZPoint;
                            XYZ vectorFromPoint1ToPoint2 = (new XYZ(point2.X, point2.Y, point1.Z) - point1).Normalize();

                            double distanceToMillimeters = UnitUtils.ConvertFromInternalUnits(distance, DisplayUnitType.DUT_MILLIMETERS);
                            if (multiple != 0
                                && !IsZero(distanceToMillimeters % multiple))
                            {
                                double footMultiple = UnitUtils.ConvertToInternalUnits(multiple, DisplayUnitType.DUT_MILLIMETERS);
                                double nearestMultiple = Math.Round(distance / footMultiple, MidpointRounding.AwayFromZero) * footMultiple;
                                double distanceToMove = distance - nearestMultiple;
                                XYZ pointToMove = vectorFromPoint1ToPoint2 * distanceToMove;

                                element.Location.Move(pointToMove);
                            }

                            if (createDimension)
                            {
                                ReferenceArray references = GetLineAndGridReferences(element, line, grid);

                                Line dimensionLine = null;
                                try
                                {
                                    dimensionLine = Line.CreateBound(point1, new XYZ(point2.X, point2.Y, point1.Z));
                                }
                                catch (Autodesk.Revit.Exceptions.ArgumentsInconsistentException)
                                {
                                }

                                if (dimensionLine != null)
                                {
                                    try
                                    {
                                        Dimension dimension = doc.Create.NewDimension(doc.ActiveView, dimensionLine, references);

                                        if (dimension.NumberOfSegments > 1)
                                        {
                                            foreach (DimensionSegment dimensionSegment in dimension.Segments)
                                            {
                                                XYZ tp = dimensionSegment.TextPosition;
                                                tp = new XYZ(tp.X + i, tp.Y + i, 0);
                                            }
                                        }
                                        else
                                        {
                                            XYZ tp = dimension.TextPosition;
                                            dimension.TextPosition = new XYZ(tp.X + i, tp.Y + i, tp.Z);
                                        }
                                    }
                                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                                    {
                                    }
                                }
                            }

                            i++;
                        }
                    }
                }

                t.Commit();
            }
        }

        private List<Line> GetSymbolGeometryLines(Element element)
        {
            List<Line> geometryLines = new List<Line>();
            GeometryElement geometryElement = element.get_Geometry(geometryOptions);
            foreach (var geometry in geometryElement)
            {
                switch (geometry)
                {
                    case GeometryInstance geometryInstance:
                        foreach (GeometryObject geometryObject in geometryInstance.GetSymbolGeometry())
                        {
                            if (geometryObject is Line currentLine)
                            {
                                if (currentLine.Reference != null)
                                {
                                    int c = 0;
                                    foreach (Line line in geometryLines)
                                        if (IsEqual(currentLine, line)
                                            || IsEqual((Line)currentLine.CreateReversed(), line)) c++;
                                    if (c == 0) geometryLines.Add(currentLine);
                                }
                            }
                        }
                        break;
                    case Line currentLine:
                        if (currentLine.Reference != null)
                        {
                            int c = 0;
                            foreach (Line line in geometryLines)
                                if (IsEqual(currentLine, line)
                                    || IsEqual((Line)currentLine.CreateReversed(), line)) c++;
                            if (c == 0) geometryLines.Add(currentLine);
                        }
                        break;
                    default:
                        break;
                }
            }

            if (geometryLines.Any())
            {
                List<Line> curveLoop = CreateCurveLoop(geometryLines.ToList());

                if (curveLoop == null || !curveLoop.Any()) return geometryLines;
                return curveLoop;
            }
            return null;
        }

        private List<Line> CreateCurveLoop(List<Line> lines)
        {
            foreach (Line line in lines)
            {
                CurveLoop curveLoop = new CurveLoop();
                curveLoop.Append(line);
                List<Line> result = new List<Line> { line };
                for (int i = 0; i < 2; i++)
                {
                    foreach (Line contiguousLine in lines)
                    {
                        int c = 0;
                        foreach (Line lineInResult in result)
                            if (IsEqual(lineInResult, contiguousLine)
                                || IsEqual(Line.CreateBound(lineInResult.GetEndPoint(1), lineInResult.GetEndPoint(0)), contiguousLine)) c++;
                        if (c != 0) continue;
                    
                        try
                        {
                            curveLoop.Append(contiguousLine);
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException)
                        {
                            try
                            {
                                curveLoop.Append(Line.CreateBound(contiguousLine.GetEndPoint(1), contiguousLine.GetEndPoint(0)));
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                continue;
                            }
                        }

                        result.Add(contiguousLine);
                    }
                }

                IList<Curve> listToSort = curveLoop.ToList();
                SortCurvesContiguous(listToSort);
                curveLoop = new CurveLoop();
                foreach (Curve curve in listToSort) curveLoop.Append(curve);

                if (!curveLoop.IsOpen()) return result;
            }
            return null;
        }

        private ReferenceArray GetLineAndGridReferences(Element element, Line line, Grid grid)
        {
            ReferenceArray references = new ReferenceArray();
            references.Append(line.Reference);

            foreach (GeometryObject geometryObject in grid.get_Geometry(geometryOptions))
                if (geometryObject is Line refLine) references.Append(refLine.Reference);

            return references;
        }

        private List<InformationAboutLineModel> GetInformationAboutLines(Element element, List<Line> geometryLines, List<Grid> grids)
        {
            XYZ point = XYZ.Zero;
            if (element is FamilyInstance)
            {
                LocationPoint locationPoint = element.Location as LocationPoint;
                point = locationPoint.Point;
            }

            List<InformationAboutLineModel> InformationAboutLineModelList = new List<InformationAboutLineModel>();
            foreach (Line geometryLine in geometryLines)
            {
                List<Grid> parallelGrids = new List<Grid>();
                foreach (Grid grid in grids)
                {
                    if (!grid.IsCurved)
                    {
                        Line gridLine = (Line)grid.Curve;
                        if (IsParallel(geometryLine.Direction, gridLine.Direction)) parallelGrids.Add(grid);
                    }
                    if (geometryLine.Direction.Z != 0) parallelGrids.Add(grid);
                }
                if (!parallelGrids.Any()) continue;

                Grid nearestGridToLine = null;
                double distanceBetweenLineAndNearestGrid = 99999;
                foreach (Grid parallelGrid in parallelGrids)
                {
                    XYZ point1 = geometryLine.GetEndPoint(0) + point;
                    XYZ point2 = parallelGrid.Curve.Project(point1).XYZPoint;
                    double currentDistance = Math.Abs(point1.DistanceTo(new XYZ(point2.X, point2.Y, point1.Z)));

                    if (distanceBetweenLineAndNearestGrid > currentDistance)
                    {
                        distanceBetweenLineAndNearestGrid = currentDistance;
                        nearestGridToLine = parallelGrid;
                    }
                }

                InformationAboutLineModel informationAboutLineModel = new InformationAboutLineModel
                {
                    CurrentLine = geometryLine,
                    NearestGridToLine = nearestGridToLine,
                    DistanceBetweenLineAndGrid = distanceBetweenLineAndNearestGrid
                };
                InformationAboutLineModelList.Add(informationAboutLineModel);

                if (geometryLine.Direction.Z != 0)
                {
                    Grid secondNearestGridToLine = null;
                    double secondDistanceBetweenLineAndNearestGrid = 99999;
                    foreach (Grid parallelGrid in parallelGrids)
                    {
                        XYZ point1 = geometryLine.GetEndPoint(0) + point;
                        XYZ point2 = parallelGrid.Curve.Project(point1).XYZPoint;
                        double currentDistance = Math.Abs(point1.DistanceTo(new XYZ(point2.X, point2.Y, point1.Z)));

                        Line gridLine1 = nearestGridToLine.Curve as Line;
                        Line gridLine2 = parallelGrid.Curve as Line;
                        
                        if (secondDistanceBetweenLineAndNearestGrid > currentDistance
                            && !IsEqual(gridLine1, gridLine2))
                        {
                            secondDistanceBetweenLineAndNearestGrid = currentDistance;
                            secondNearestGridToLine = parallelGrid;
                        }
                    }

                    InformationAboutLineModel secondInformationAboutLineModel = new InformationAboutLineModel
                    {
                        CurrentLine = geometryLine,
                        NearestGridToLine = secondNearestGridToLine,
                        DistanceBetweenLineAndGrid = secondDistanceBetweenLineAndNearestGrid
                    };
                    InformationAboutLineModelList.Add(secondInformationAboutLineModel);
                }
            }

            if (InformationAboutLineModelList.Any())
            {
                InformationAboutLineModelList.Sort((x, y) => x.DistanceBetweenLineAndGrid.CompareTo(y.DistanceBetweenLineAndGrid));
                return InformationAboutLineModelList;
            }
            return null;
        }

        public const double _eps = 1.0e-9;
        public static bool IsEqual(Line a, Line b)
        {
            XYZ pa = a.GetEndPoint(0);
            XYZ qa = a.GetEndPoint(1);
            XYZ pb = b.GetEndPoint(0);
            XYZ qb = b.GetEndPoint(1);
            XYZ va = qa - pa;
            XYZ vb = qb - pb;

            double ang_a = Math.Atan2(va.Y, va.X);
            double ang_b = Math.Atan2(vb.Y, vb.X);

            bool d = Compare(ang_a, ang_b);

            if (d)
            {
                // Compare distance of unbounded line to origin

                double da = (qa.X * pa.Y - qa.Y * pa.Y)
                  / va.GetLength();

                double db = (qb.X * pb.Y - qb.Y * pb.Y)
                  / vb.GetLength();

                d = Compare(da, db);

                if (d)
                {
                    // Compare distance of start point to origin

                    d = Compare(pa.GetLength(), pb.GetLength());

                    if (d)
                    {
                        // Compare distance of end point to origin

                        d = Compare(qa.GetLength(), qb.GetLength());
                    }
                }
            }

            return d;
        }

        public static bool Compare(XYZ p, XYZ q)
        {
            bool d = Compare(p.X, q.X);

            if (d)
            {
                d = Compare(p.Y, q.Y);

                if (d)
                {
                    d = Compare(p.Z, q.Z);
                }
            }
            return d;
        }

        public static bool Compare(double a, double b, double tolerance = _eps)
        {
            return IsEqual(a, b, tolerance);
        }

        public static bool IsEqual(double a, double b, double tolerance = _eps)
        {
            return IsZero(b - a, tolerance);
        }

        public static bool IsZero(double a, double tolerance = _eps)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsParallel(XYZ p, XYZ q)
        {
            return p.CrossProduct(q).IsZeroLength();
        }

        const double _inch = 1.0 / 12.0;
        const double _sixteenth = _inch / 16.0;

        public static void SortCurvesContiguous(IList<Curve> curves)
        {
            int n = curves.Count;

            // Walk through each curve (after the first) 
            // to match up the curves in order

            for (int i = 0; i < n; ++i)
            {
                Curve curve = curves[i];
                XYZ endPoint = curve.GetEndPoint(1);
                XYZ p;

                // Find curve with start point = end point

                bool found = (i + 1 >= n);

                for (int j = i + 1; j < n; ++j)
                {
                    p = curves[j].GetEndPoint(0);

                    // If there is a match end->start, 
                    // this is the next curve

                    if (_sixteenth > p.DistanceTo(endPoint))
                    {

                        if (i + 1 != j)
                        {
                            Curve tmp = curves[i + 1];
                            curves[i + 1] = curves[j];
                            curves[j] = tmp;
                        }
                        found = true;
                        break;
                    }

                    p = curves[j].GetEndPoint(1);

                    // If there is a match end->end, 
                    // reverse the next curve

                    if (_sixteenth > p.DistanceTo(endPoint))
                    {
                        if (i + 1 == j)
                        {
                            curves[i + 1] = Line.CreateBound(curves[j].GetEndPoint(1), curves[j].GetEndPoint(0));
                        }
                        else
                        {

                            Curve tmp = curves[i + 1];
                            curves[i + 1] = Line.CreateBound(curves[j].GetEndPoint(1), curves[j].GetEndPoint(0));
                            curves[j] = tmp;
                        }
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new Exception("SortCurvesContiguous:"
                      + " non-contiguous input curves");
                }
            }
        }
    }
}
