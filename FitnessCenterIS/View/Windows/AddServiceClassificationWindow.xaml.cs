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
    /// Interaction logic for AddServiceClassificationWindow.xaml
    /// </summary>
    public partial class AddServiceClassificationWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private ServiceClassifications _currentServiceClassification;
        public ServiceClassifications NewClassification { get; private set; }
        private bool _isEditMode = false;

        public AddServiceClassificationWindow(BDFitnessClubDipEntities dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _currentServiceClassification = new ServiceClassifications();
            DataContext = _currentServiceClassification;
            LoadServiceClassifications();
        }

        private void LoadServiceClassifications()
        {
            ServiceClassificationsDataGrid.ItemsSource = _dbContext.ServiceClassifications.ToList();
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
            if (string.IsNullOrWhiteSpace(_currentServiceClassification.Name))
            {
                MessageBox.Show("Введите название классификации услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (_isEditMode)
                {
                    var existingClassification = _dbContext.ServiceClassifications.Find(_currentServiceClassification.ServiceClassificationID);
                    if (existingClassification != null)
                    {
                        existingClassification.Name = _currentServiceClassification.Name;
                        existingClassification.Description = _currentServiceClassification.Description;
                    }
                }
                else
                {
                    _dbContext.ServiceClassifications.Add(_currentServiceClassification);
                }

                _dbContext.SaveChanges();
                LoadServiceClassifications();

                MessageBox.Show(_isEditMode ? "Классификация услуги успешно обновлена" : "Новая классификация услуги успешно добавлена",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении классификации услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewClassificationButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void ServiceClassificationsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServiceClassificationsDataGrid.SelectedItem is ServiceClassifications selectedClassification)
            {
                _currentServiceClassification = new ServiceClassifications
                {
                    ServiceClassificationID = selectedClassification.ServiceClassificationID,
                    Name = selectedClassification.Name,
                    Description = selectedClassification.Description
                };
                DataContext = _currentServiceClassification;
                _isEditMode = true;
                SaveButton.Content = "Обновить";
            }
        }

        private void ResetForm()
        {
            _currentServiceClassification = new ServiceClassifications();
            DataContext = _currentServiceClassification;
            _isEditMode = false;
            SaveButton.Content = "Сохранить";
            ServiceClassificationsDataGrid.SelectedItem = null;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentServiceClassification != null && _currentServiceClassification.ServiceClassificationID != 0)
            {
                MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите удалить эту классификацию услуги?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var classificationToDelete = _dbContext.ServiceClassifications.Find(_currentServiceClassification.ServiceClassificationID);
                        if (classificationToDelete != null)
                        {
                            _dbContext.ServiceClassifications.Remove(classificationToDelete);
                            _dbContext.SaveChanges();
                            LoadServiceClassifications();
                            ResetForm();
                            MessageBox.Show("Классификация услуги успешно удалена", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении классификации услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите классификацию услуги для удаления", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

}
