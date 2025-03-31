using System;
using System.Collections.Generic;
using System.Data.Entity;
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
    public partial class AddServiceWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private Services _service;
        private bool _isEditMode;

        public AddServiceWindow(BDFitnessClubDipEntities dbContext, Services service = null)
        {
            InitializeComponent();
            _dbContext = dbContext;

            LoadComboBoxes();

            if (service != null)
            {
                _service = service;
                _isEditMode = true;
                WindowTitle.Text = "Редактирование услуги";
            }
            else
            {
                _service = new Services
                {
                    StatusService = "Активен",
                };
                _isEditMode = false;
                WindowTitle.Text = "Новая услуга";
            }

            DataContext = _service;
        }

        private void LoadComboBoxes()
        {
            ServiceTypeComboBox.ItemsSource = _dbContext.ServiceTypes.ToList();
            ServiceClassificationComboBox.ItemsSource = _dbContext.ServiceClassifications.ToList();

            // Очищаем существующие элементы в StatusComboBox
            StatusComboBox.Items.Clear();

            // Добавляем русские значения
            StatusComboBox.Items.Add("Активен");
            StatusComboBox.Items.Add("Не активен");
        }



        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateService())
            {
                try
                {
                    if (_isEditMode)
                    {
                        _dbContext.Entry(_service).State = EntityState.Modified;
                    }
                    else
                    {
                        _dbContext.Services.Add(_service);
                    }

                    _dbContext.SaveChanges();
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateService()
        {
            if (string.IsNullOrWhiteSpace(_service.Name))
            {
                MessageBox.Show("Введите название услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.Price <= 0)
            {
                MessageBox.Show("Цена должна быть больше нуля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.SeviceTypeID == 0)
            {
                MessageBox.Show("Выберите тип услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.ServiceClassificationID == 0)
            {
                MessageBox.Show("Выберите классификацию услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void AddServiceClassification_Click(object sender, RoutedEventArgs e)
        {
            var addClassificationWindow = new AddServiceClassificationWindow(_dbContext);
            if (addClassificationWindow.ShowDialog() == true)
            {
                LoadComboBoxes();
                ServiceClassificationComboBox.SelectedItem = addClassificationWindow.ServiceClassificationsDataGrid;
            }
        }

        private void AddServiceType_Click(object sender, RoutedEventArgs e)
        {
            var addTypeWindow = new AddServiceTypeWindow(_dbContext);
            if (addTypeWindow.ShowDialog() == true)
            {
                LoadComboBoxes();
                ServiceTypeComboBox.SelectedItem = addTypeWindow.NewType;
            }
        }
    }
}
