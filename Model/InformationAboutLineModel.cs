using Autodesk.Revit.DB;

namespace MultipleDimensionToNearestGrid
{
    public class InformationAboutLineModel : ModelBase
    {
        public Line CurrentLine { get; set; }
        public Grid NearestGridToLine { get; set; }
        public double DistanceBetweenLineAndGrid { get; set; }
    }
}
