using Autodesk.Revit.UI;
using System;

namespace MultipleDimensionToNearestGrid
{
    public class RevitEvent : IExternalEventHandler
    {
        private Action<UIApplication> _action;
        private readonly ExternalEvent _externalEvent;
        public RevitEvent()
        {
            _externalEvent = ExternalEvent.Create(this);
        }
        public void Run(Action<UIApplication> action)
        {
            _action = action;
            _externalEvent.Raise();
        }
        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke(app);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: Failed to execute current command in Revit flow.\n{ex.Message}");
            }
        }
        public string GetName() => nameof(RevitEvent);
    }
}
