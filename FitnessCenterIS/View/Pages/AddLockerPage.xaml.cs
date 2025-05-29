using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Pages
{
    public partial class AddLockerPage : Page
    {
        private int? _editingLockerId = null;
        private bool _isEditing = false;

        public AddLockerPage()
        {
            InitializeComponent();
        }

        private void AddLockerPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLockerRoomTypes();
            LoadLockers();
        }

        private void LoadLockerRoomTypes()
        {
            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var lockerRoomTypes = context.LockerRoomTypes
                        .OrderBy(lrt => lrt.Name)
                        .ToList();

                    LockerRoomTypeComboBox.ItemsSource = lockerRoomTypes;
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при загрузке типов раздевалок: {ex.Message}");
            }
        }

        private void LoadLockers()
        {
            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var lockers = context.Lockers
                        .OrderBy(l => l.KeyNumber)
                        .Select(l => new
                        {
                            l.LockerID,
                            l.KeyNumber,
                            LockerRoomTypeName = l.LockerRoomTypes != null ? l.LockerRoomTypes.Name : "Не указан",
                            l.IsAvailable,
                            // Проверяем, используется ли шкафчик в активных посещениях
                            IsInUse = context.Attendances
                                .Any(a => a.LockerID == l.LockerID && a.ExitDateTime == null)
                        })
                        .ToList();

                    LockersDataGrid.ItemsSource = lockers;
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при загрузке шкафчиков: {ex.Message}");
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    // Проверка на дублирование номера ключа
                    var existingLocker = context.Lockers
                        .FirstOrDefault(l => l.KeyNumber.ToLower().Trim() == KeyNumberTextBox.Text.ToLower().Trim());

                    if (existingLocker != null)
                    {
                        ShowValidationMessage($"Шкафчик с номером ключа \"{KeyNumberTextBox.Text.Trim()}\" уже существует!");
                        KeyNumberTextBox.Focus();
                        return;
                    }

                    // Создание нового шкафчика
                    var selectedLockerRoomType = (LockerRoomTypes)LockerRoomTypeComboBox.SelectedItem;

                    var newLocker = new Lockers
                    {
                        KeyNumber = KeyNumberTextBox.Text.Trim(),
                        LockerRoomTypeID = selectedLockerRoomType.LockerRoomTypeID,
                        IsAvailable = true // По умолчанию шкафчик доступен при создании
                    };

                    context.Lockers.Add(newLocker);
                    context.SaveChanges();

                    MessageBox.Show($"Шкафчик с номером ключа \"{newLocker.KeyNumber}\" успешно добавлен!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    ClearForm();
                    LoadLockers();
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при добавлении шкафчика: {ex.Message}");
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput() || !_editingLockerId.HasValue)
                return;

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var existingLocker = context.Lockers.FirstOrDefault(l => l.LockerID == _editingLockerId.Value);
                    if (existingLocker != null)
                    {
                        // Проверка на дублирование номера ключа (исключая текущий шкафчик)
                        var duplicateLocker = context.Lockers
                            .FirstOrDefault(l => l.KeyNumber.ToLower().Trim() == KeyNumberTextBox.Text.ToLower().Trim()
                                               && l.LockerID != _editingLockerId.Value);

                        if (duplicateLocker != null)
                        {
                            ShowValidationMessage($"Шкафчик с номером ключа \"{KeyNumberTextBox.Text.Trim()}\" уже существует!");
                            KeyNumberTextBox.Focus();
                            return;
                        }

                        var selectedLockerRoomType = (LockerRoomTypes)LockerRoomTypeComboBox.SelectedItem;

                        existingLocker.KeyNumber = KeyNumberTextBox.Text.Trim();
                        existingLocker.LockerRoomTypeID = selectedLockerRoomType.LockerRoomTypeID;
                        // IsAvailable не изменяем при редактировании, так как это управляется системой посещаемости

                        context.SaveChanges();
                        MessageBox.Show($"Шкафчик с номером ключа \"{existingLocker.KeyNumber}\" успешно обновлен!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        ClearForm();
                        LoadLockers();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при обновлении шкафчика: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (LockersDataGrid.SelectedItem == null)
            {
                ShowValidationMessage("Выберите шкафчик для удаления.");
                return;
            }

            var selectedLocker = (dynamic)LockersDataGrid.SelectedItem;
            int lockerId = selectedLocker.LockerID;
            string keyNumber = selectedLocker.KeyNumber;

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    // Проверяем, используется ли шкафчик в активных посещениях
                    var activeAttendances = context.Attendances
                        .Where(a => a.LockerID == lockerId && a.ExitDateTime == null)
                        .ToList();

                    if (activeAttendances.Any())
                    {
                        var errorMessage = $"Невозможно удалить шкафчик с номером ключа \"{keyNumber}\"!\n\n" +
                                         $"Шкафчик используется в {activeAttendances.Count} активных посещениях.\n\n" +
                                         "Для удаления шкафчика необходимо:\n" +
                                         "1. Завершить все активные посещения\n" +
                                         "2. Освободить шкафчик";

                        MessageBox.Show(errorMessage, "Шкафчик используется",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить шкафчик с номером ключа \"{keyNumber}\"?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var locker = context.Lockers.FirstOrDefault(l => l.LockerID == lockerId);
                        if (locker != null)
                        {
                            context.Lockers.Remove(locker);
                            context.SaveChanges();

                            MessageBox.Show($"Шкафчик с номером ключа \"{keyNumber}\" успешно удален!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            LoadLockers();

                            // Если удаляемый шкафчик редактировался, очищаем форму
                            if (_editingLockerId == lockerId)
                            {
                                ClearForm();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при удалении шкафчика: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLockers();
            ClearForm();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var filteredLockers = context.Lockers
                        .Where(l => l.KeyNumber.ToLower().Contains(searchText) ||
                                   (l.LockerRoomTypes != null && l.LockerRoomTypes.Name.ToLower().Contains(searchText)))
                        .OrderBy(l => l.KeyNumber)
                        .Select(l => new
                        {
                            l.LockerID,
                            l.KeyNumber,
                            LockerRoomTypeName = l.LockerRoomTypes != null ? l.LockerRoomTypes.Name : "Не указан",
                            l.IsAvailable,
                            IsInUse = context.Attendances
                                .Any(a => a.LockerID == l.LockerID && a.ExitDateTime == null)
                        })
                        .ToList();

                    LockersDataGrid.ItemsSource = filteredLockers;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска: {ex.Message}");
            }
        }

        private void LockersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LockersDataGrid.SelectedItem != null)
            {
                var selectedLocker = (dynamic)LockersDataGrid.SelectedItem;

                _editingLockerId = selectedLocker.LockerID;
                _isEditing = true;

                KeyNumberTextBox.Text = selectedLocker.KeyNumber ?? "";

                // Устанавливаем тип раздевалки
                var lockerRoomTypeName = selectedLocker.LockerRoomTypeName;
                foreach (LockerRoomTypes item in LockerRoomTypeComboBox.Items)
                {
                    if (item.Name == lockerRoomTypeName)
                    {
                        LockerRoomTypeComboBox.SelectedItem = item;
                        break;
                    }
                }

                FormHeaderTextBlock.Text = "Редактировать шкафчик";
                AddButton.Visibility = Visibility.Collapsed;
                UpdateButton.Visibility = Visibility.Visible;

                HideValidationMessage();
            }
        }

        private void LockersDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(KeyNumberTextBox.Text))
            {
                ShowValidationMessage("Пожалуйста, введите номер ключа.");
                KeyNumberTextBox.Focus();
                return false;
            }

            if (LockerRoomTypeComboBox.SelectedItem == null)
            {
                ShowValidationMessage("Пожалуйста, выберите тип раздевалки.");
                LockerRoomTypeComboBox.Focus();
                return false;
            }

            HideValidationMessage();
            return true;
        }

        private void ClearForm()
        {
            KeyNumberTextBox.Clear();
            LockerRoomTypeComboBox.SelectedItem = null;
            _editingLockerId = null;
            _isEditing = false;
            FormHeaderTextBlock.Text = "Добавить новый шкафчик";
            AddButton.Visibility = Visibility.Visible;
            UpdateButton.Visibility = Visibility.Collapsed;
            LockersDataGrid.SelectedItem = null;
            HideValidationMessage();
        }

        private void ShowValidationMessage(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private void HideValidationMessage()
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;
        }
    }
}
