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
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for AddServiceTypeWindow.xaml
    /// </summary>
    public partial class AddServiceTypeWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;

        private ServiceTypes _currentServiceType;
        public ServiceTypes NewType { get; private set; }
        private bool _isEditMode = false;

        public AddServiceTypeWindow(BDFitnessClubDipEntities dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _currentServiceType = new ServiceTypes();
            DataContext = _currentServiceType;
            LoadServiceTypes();
        }
        private void LoadServiceTypes()
        {
            ServiceTypesDataGrid.ItemsSource = _dbContext.ServiceTypes.ToList();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentServiceType.Name))
            {
                MessageBox.Show("Введите название типа услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (_isEditMode)
                {
                    var existingType = _dbContext.ServiceTypes.Find(_currentServiceType.ServiceTypeID);
                    if (existingType != null)
                    {
                        existingType.Name = _currentServiceType.Name;
                        existingType.Description = _currentServiceType.Description;
                    }
                }
                else
                {
                    _dbContext.ServiceTypes.Add(_currentServiceType);
                }

                _dbContext.SaveChanges();
                LoadServiceTypes();

                MessageBox.Show(_isEditMode ? "Тип услуги успешно обновлен" : "Новый тип услуги успешно добавлен",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении типа услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewTypeButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void ServiceTypesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServiceTypesDataGrid.SelectedItem is ServiceTypes selectedType)
            {
                _currentServiceType = new ServiceTypes
                {
                    ServiceTypeID = selectedType.ServiceTypeID,
                    Name = selectedType.Name,
                    Description = selectedType.Description
                };
                DataContext = _currentServiceType;
                _isEditMode = true;
                SaveButton.Content = "Обновить";
            }
        }

        private void ResetForm()
        {
            _currentServiceType = new ServiceTypes();
            DataContext = _currentServiceType;
            _isEditMode = false;
            SaveButton.Content = "Сохранить";
            ServiceTypesDataGrid.SelectedItem = null;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentServiceType != null && _currentServiceType.ServiceTypeID != 0)
            {
                MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите удалить этот тип услуги?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var typeToDelete = _dbContext.ServiceTypes.Find(_currentServiceType.ServiceTypeID);
                        if (typeToDelete != null)
                        {
                            _dbContext.ServiceTypes.Remove(typeToDelete);
                            _dbContext.SaveChanges();
                            LoadServiceTypes();
                            ResetForm();
                            MessageBox.Show("Тип услуги успешно удален", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении типа услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите тип услуги для удаления", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

}
