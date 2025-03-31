using FitnessCenterIS.View.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for ServicesPage.xaml
    /// </summary>
    public partial class ServicesPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private List<Services> _allServices;

        public ServicesPage()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            UpdateServicesGrid();
        }

        private void UpdateServicesGrid()
        {
            _allServices = _dbContext.Services.ToList();
            ServicesDataGrid.ItemsSource = _allServices;
        }

        private void AddService_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddServiceWindow(_dbContext);
            if (addWindow.ShowDialog() == true)
            {
                UpdateServicesGrid();
            }
        }

        private void EditService_Click(object sender, RoutedEventArgs e)
        {
            var selectedService = ServicesDataGrid.SelectedItem as Services;
            if (selectedService != null)
            {
                var editWindow = new AddServiceWindow(_dbContext, selectedService);
                if (editWindow.ShowDialog() == true)
                {
                    UpdateServicesGrid();
                }
            }
            else
            {
                MessageBox.Show("Выберите услугу для редактирования");
            }
        }

        private void DeleteService_Click(object sender, RoutedEventArgs e)
        {
            var selectedService = ServicesDataGrid.SelectedItem as Services;
            if (selectedService != null)
            {
                var result = MessageBox.Show("Вы уверены, что хотите удалить эту услугу?", "Подтверждение удаления", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    _dbContext.Services.Remove(selectedService);
                    _dbContext.SaveChanges();
                    UpdateServicesGrid();
                }
            }
            else
            {
                MessageBox.Show("Выберите услугу для удаления");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            var filteredList = _allServices.Where(s =>
                s.Name.ToLower().Contains(searchText) ||
                s.Description?.ToLower().Contains(searchText) == true ||
                s.Price.ToString().Contains(searchText)).ToList();
            ServicesDataGrid.ItemsSource = filteredList;
        }

        private void ServicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработка выбора услуги
        }

        private void ServicesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ServicesDataGrid.SelectedItem != null)
            {
                EditService_Click(sender, e);
            }
        }
    }
}
