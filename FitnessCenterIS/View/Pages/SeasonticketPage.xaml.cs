using FitnessCenterIS.Model;
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
    /// Interaction logic for SeasonticketPage.xaml
    /// </summary>
    public partial class SeasonticketPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private List<Seasontickets> _allSeasontickets;

        public SeasonticketPage()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            UpdateSeasonticketGrid();
        }

        private void UpdateSeasonticketGrid()
        {
            _allSeasontickets = _dbContext.Seasontickets.ToList();
            SeasonticketDataGrid.ItemsSource = _allSeasontickets;
        }

        private void AddSeasonticket_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddSeasonticketWindow(_dbContext);
            if (addWindow.ShowDialog() == true)
            {
                UpdateSeasonticketGrid();
            }
        }

        private void EditSeasonticket_Click(object sender, RoutedEventArgs e)
        {
            var selectedSeasonticket = SeasonticketDataGrid.SelectedItem as Seasontickets;
            if (selectedSeasonticket != null)
            {
                var editWindow = new AddSeasonticketWindow(_dbContext, selectedSeasonticket);
                if (editWindow.ShowDialog() == true)
                {
                    UpdateSeasonticketGrid();
                }
            }
            else
            {
                MessageBox.Show("Выберите абонемент для редактирования", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteSeasonticket_Click(object sender, RoutedEventArgs e)
        {
            var selectedSeasonticket = SeasonticketDataGrid.SelectedItem as Seasontickets;
            if (selectedSeasonticket != null)
            {
                var result = MessageBox.Show("Вы уверены, что хотите удалить этот абонемент?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _dbContext.Seasontickets.Remove(selectedSeasonticket);
                        _dbContext.SaveChanges();
                        UpdateSeasonticketGrid();
                        MessageBox.Show("Абонемент успешно удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении абонемента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите абонемент для удаления", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            var filteredList = _allSeasontickets.Where(s =>
                s.Name.ToLower().Contains(searchText) ||
                s.Description?.ToLower().Contains(searchText) == true ||
                s.Price.ToString().Contains(searchText) ||
                s.ValidityDuration.ToString().Contains(searchText) ||
                s.StatusSeasonticket.ToLower().Contains(searchText)).ToList();
            SeasonticketDataGrid.ItemsSource = filteredList;
        }

        private void SeasonticketDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SeasonticketDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SeasonticketDataGrid.SelectedItem != null)
            {
                EditSeasonticket_Click(sender, e);
            }
        }
    }

}
