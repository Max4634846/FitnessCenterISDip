using FitnessCenterIS.Model;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddEditGroupWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private Groups _group;
        private bool _isEditMode;

        public AddEditGroupWindow(BDFitnessClubDipEntities dbContext, Groups group = null)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _group = group;
            _isEditMode = group != null;

            InitializeWindow();
            LoadServices();
            LoadGroupData();
            UpdateTitle();
        }

        private void InitializeWindow()
        {
            // Если это режим редактирования, выбираем статус по умолчанию
            if (_isEditMode)
            {
                StatusActivityComboBox.SelectedIndex = _group.StatusActivity == "Активен" ? 0 : 1;
            }
            else
            {
                StatusActivityComboBox.SelectedIndex = 0; // По умолчанию "Активно"
            }
        }

        private void LoadServices()
        {
            try
            {
                var services = _dbContext.Services
                    .Where(s => s.StatusService == "Активен")
                    .ToList();
                ServiceComboBox.ItemsSource = services;
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки услуг: {ex.Message}");
            }
        }

        private void LoadGroupData()
        {
            if (_isEditMode && _group != null)
            {
                NameTextBox.Text = _group.Name;
                DescriptionTextBox.Text = _group.Description;
                LimitCapacityTextBox.Text = _group.LimitCapacity?.ToString();
                DiscountTextBox.Text = _group.Discount?.ToString();

                // Безопасный выбор услуги
                if (_group.ServiceID.HasValue)
                {
                    var service = ServiceComboBox.Items.Cast<Services>()
                        .FirstOrDefault(s => s.ServiceID == _group.ServiceID.Value);
                    if (service != null)
                    {
                        ServiceComboBox.SelectedItem = service;
                    }
                }

                // Безопасный выбор статуса
                if (!string.IsNullOrEmpty(_group.StatusActivity))
                {
                    StatusActivityComboBox.SelectedItem = StatusActivityComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Content.ToString() == _group.StatusActivity);
                }
            }
        }

        private void UpdateTitle()
        {
            TitleTextBlock.Text = _isEditMode ? "Редактирование группы" : "Новая группа";
            Title = TitleTextBlock.Text;
        }

        private bool ValidateFields()
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                ShowValidationError("Введите название группы");
                return false;
            }

            if (ServiceComboBox.SelectedItem == null)
            {
                ShowValidationError("Выберите услугу");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(LimitCapacityTextBox.Text))
            {
                if (!int.TryParse(LimitCapacityTextBox.Text, out int limit) || limit <= 0)
                {
                    ShowValidationError("Укажите корректное максимальное количество участников");
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(DiscountTextBox.Text))
            {
                if (!decimal.TryParse(DiscountTextBox.Text, out decimal discount) || discount < 0 || discount > 100)
                {
                    ShowValidationError("Укажите корректную скидку (от 0 до 100)");
                    return false;
                }
            }

            return true;
        }

        private void ShowValidationError(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFields()) return;

            try
            {
                if (_isEditMode)
                {
                    UpdateExistingGroup();
                }
                else
                {
                    CreateNewGroup();
                }

                _dbContext.SaveChanges();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения группы: {ex.Message}");
            }
        }

        private void CreateNewGroup()
        {
            // Безопасное получение ServiceID
            if (ServiceComboBox.SelectedItem is Services selectedService)
            {
                var newGroup = new Groups
                {
                    Name = NameTextBox.Text.Trim(),
                    Description = DescriptionTextBox.Text.Trim(),
                    ServiceID = selectedService.ServiceID,
                    LimitCapacity = string.IsNullOrWhiteSpace(LimitCapacityTextBox.Text) ?
                        (int?)null : int.Parse(LimitCapacityTextBox.Text),
                    Discount = string.IsNullOrWhiteSpace(DiscountTextBox.Text) ?
                        (decimal?)null : decimal.Parse(DiscountTextBox.Text),
                    StatusActivity = ((ComboBoxItem)StatusActivityComboBox.SelectedItem).Content.ToString()
                };

                _dbContext.Groups.Add(newGroup);
            }
            else
            {
                throw new Exception("Услуга не выбрана");
            }
        }

        private void UpdateExistingGroup()
        {
            if (_group != null)
            {
                _group.Name = NameTextBox.Text.Trim();
                _group.Description = DescriptionTextBox.Text.Trim();

                // Безопасное получение ServiceID
                if (ServiceComboBox.SelectedItem is Services selectedService)
                {
                    _group.ServiceID = selectedService.ServiceID;
                }
                else
                {
                    throw new Exception("Услуга не выбрана");
                }

                _group.LimitCapacity = string.IsNullOrWhiteSpace(LimitCapacityTextBox.Text) ?
                    (int?)null : int.Parse(LimitCapacityTextBox.Text);

                _group.Discount = string.IsNullOrWhiteSpace(DiscountTextBox.Text) ?
                    (decimal?)null : decimal.Parse(DiscountTextBox.Text);

                // Безопасное получение статуса из ComboBox
                if (StatusActivityComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    _group.StatusActivity = selectedItem.Content.ToString();
                }
                else
                {
                    throw new Exception("Статус не выбран");
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}