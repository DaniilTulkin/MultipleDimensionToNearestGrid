using Autodesk.Revit.DB;

namespace MultipleDimensionToNearestGrid
{
    public class ProperView
    {
        /// <summary>
        /// Permited view types.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <returns></returns>
        public static bool PermitedView(View view)
        {
            ViewType viewType = view.ViewType;
            switch (viewType)
            {
                case ViewType.FloorPlan:
                    return true;
                case ViewType.EngineeringPlan:
                    return true;
                case ViewType.AreaPlan:
                    return true;
                case ViewType.CeilingPlan:
                    return true;
                default:
                    return false;
            }
        }
    }
}
