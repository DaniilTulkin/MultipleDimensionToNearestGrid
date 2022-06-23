using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MultipleDimensionToNearestGrid
{
    public class MainWindowViewModel : ModelBase
    {
        private MainWindowModelService MainWindowModelService;
        public Action CloseAction { get; set; }

        private ObservableCollection<Element> families = new ObservableCollection<Element>();
        public ObservableCollection<Element> Families
        {
            get
            {
                return families;
            }
            set
            {
                families = value;
                OnPropertyChanged("Families");
            }
        }

        private Element selectedFamily;
        public Element SelectedFamily
        {
            get
            {
                return selectedFamily;
            }
            set
            {
                selectedFamily = value;
                OnPropertyChanged("SelectedFamily");
            }
        }

        private int multiple;
        public int Multiple
        {
            get
            {
                return multiple;
            }
            set
            {
                multiple = value;
                OnPropertyChanged("Multiple");
            }
        }

        private bool createDimension;
        public bool CreateDimension
        {
            get
            {
                return createDimension;
            }
            set
            {
                createDimension = value;
                OnPropertyChanged("CreateDimension");
            }
        }

        public MainWindowViewModel(UIApplication app)
        {
            MainWindowModelService = new MainWindowModelService(app);
            MainWindowModelService.GetFamilyTypesOnCurrentView(Families);
        }

        public ICommand btnOK => new RelayCommandWithoutParameter(OnbtnOK);
        private void OnbtnOK()
        {
            MainWindowModelService.MultipleDimentionToNearestGrid(selectedFamily, multiple,createDimension);
            CloseAction();
        }

        public ICommand btnCancel => new RelayCommandWithoutParameter(OnbtnCancel);
        private void OnbtnCancel()
        {
            CloseAction();
        }
    }
}
