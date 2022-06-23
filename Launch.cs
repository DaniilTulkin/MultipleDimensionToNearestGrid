using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;

namespace MultipleDimensionToNearestGrid.Model
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MultipleDimensionToNearestGrid : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            try
            {
                MainWindow mainWindow = new MainWindow(app);
                mainWindow?.ShowDialog();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: Failed to run main window of the plugin.\n{ex.Message}");
            }
        }


        public string GetName() => nameof(MultipleDimensionToNearestGrid);
    }
}
