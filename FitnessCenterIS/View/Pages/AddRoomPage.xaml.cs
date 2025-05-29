using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Pages
{
    public partial class AddRoomPage : Page
    {
        private int? _editingRoomId = null;
        private bool _isEditing = false;

        public AddRoomPage()
        {
            InitializeComponent();
        }

        private void AddRoomPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRooms();
        }

        private void LoadRooms()
        {
            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var rooms = context.Rooms
                        .OrderBy(r => r.Name)
                        .Select(r => new
                        {
                            r.RoomID,
                            r.Name,
                            Description = string.IsNullOrEmpty(r.Description) ? "Описание отсутствует" : r.Description,
                            // Добавляем информацию об использовании
                            ActiveSchedulesCount = context.Schedules
                                .Count(s => s.RoomID == r.RoomID &&
                                           (s.ScheduleStatus == "Активно" || s.ScheduleStatus == "Запланировано")),
                            IsInUse = context.Schedules
                                .Any(s => s.RoomID == r.RoomID &&
                                         (s.ScheduleStatus == "Активно" || s.ScheduleStatus == "Запланировано"))
                        })
                        .ToList();

                    RoomsDataGrid.ItemsSource = rooms;

                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при загрузке комнат: {ex.Message}");
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
                    // Проверка на дублирование названия
                    var existingRoom = context.Rooms
                        .FirstOrDefault(r => r.Name.ToLower().Trim() == RoomNameTextBox.Text.ToLower().Trim());

                    if (existingRoom != null)
                    {
                        ShowValidationMessage($"Комната с названием \"{RoomNameTextBox.Text.Trim()}\" уже существует!");
                        RoomNameTextBox.Focus();
                        return;
                    }

                    // Создание новой комнаты
                    var newRoom = new Rooms
                    {
                        Name = RoomNameTextBox.Text.Trim(),
                        Description = string.IsNullOrWhiteSpace(RoomDescriptionTextBox.Text)
                            ? null
                            : RoomDescriptionTextBox.Text.Trim()
                    };

                    context.Rooms.Add(newRoom);
                    context.SaveChanges();

                    MessageBox.Show($"Комната \"{newRoom.Name}\" успешно добавлена!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    ClearForm();
                    LoadRooms();
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при добавлении комнаты: {ex.Message}");
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput() || !_editingRoomId.HasValue)
                return;

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var existingRoom = context.Rooms.FirstOrDefault(r => r.RoomID == _editingRoomId.Value);
                    if (existingRoom != null)
                    {
                        // Проверка на дублирование названия (исключая текущую комнату)
                        var duplicateRoom = context.Rooms
                            .FirstOrDefault(r => r.Name.ToLower().Trim() == RoomNameTextBox.Text.ToLower().Trim()
                                               && r.RoomID != _editingRoomId.Value);

                        if (duplicateRoom != null)
                        {
                            ShowValidationMessage($"Комната с названием \"{RoomNameTextBox.Text.Trim()}\" уже существует!");
                            RoomNameTextBox.Focus();
                            return;
                        }

                        existingRoom.Name = RoomNameTextBox.Text.Trim();
                        existingRoom.Description = string.IsNullOrWhiteSpace(RoomDescriptionTextBox.Text)
                            ? null
                            : RoomDescriptionTextBox.Text.Trim();

                        context.SaveChanges();
                        MessageBox.Show($"Комната \"{existingRoom.Name}\" успешно обновлена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        ClearForm();
                        LoadRooms();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при обновлении комнаты: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsDataGrid.SelectedItem == null)
            {
                ShowValidationMessage("Выберите комнату для удаления.");
                return;
            }

            var selectedRoom = (dynamic)RoomsDataGrid.SelectedItem;
            int roomId = selectedRoom.RoomID;
            string roomName = selectedRoom.Name;

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    // Проверяем, используется ли комната в активных расписаниях
                    var activeSchedules = context.Schedules
                        .Where(s => s.RoomID == roomId &&
                                   (s.ScheduleStatus == "Активно" || s.ScheduleStatus == "Запланировано"))
                        .ToList();

                    if (activeSchedules.Any())
                    {
                        var scheduleDetails = activeSchedules
                            .Take(5) // Показываем первые 5 записей
                            .Select(s => $"• {s.Title} ({s.StartDateTime:dd.MM.yyyy HH:mm})")
                            .ToList();

                        string scheduleList = string.Join("\n", scheduleDetails);
                        if (activeSchedules.Count > 5)
                        {
                            scheduleList += $"\n... и еще {activeSchedules.Count - 5} записей";
                        }

                        var errorMessage = $"Невозможно удалить комнату \"{roomName}\"!\n\n" +
                                         $"Комната используется в {activeSchedules.Count} активных записях расписания:\n\n" +
                                         scheduleList + "\n\n" +
                                         "Для удаления комнаты необходимо:\n" +
                                         "1. Отменить или перенести все активные занятия\n" +
                                         "2. Изменить статус расписаний на \"Отменено\"\n" +
                                         "3. Или назначить другую комнату для этих занятий";

                        MessageBox.Show(errorMessage, "Комната используется в расписании",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить комнату \"{roomName}\"?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var room = context.Rooms.FirstOrDefault(r => r.RoomID == roomId);
                        if (room != null)
                        {
                            context.Rooms.Remove(room);
                            context.SaveChanges();

                            MessageBox.Show($"Комната \"{roomName}\" успешно удалена!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            LoadRooms();

                            // Если удаляемая комната редактировалась, очищаем форму
                            if (_editingRoomId == roomId)
                            {
                                ClearForm();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Ошибка при удалении комнаты: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRooms();
            ClearForm();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();

            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var filteredRooms = context.Rooms
                        .Where(r => r.Name.ToLower().Contains(searchText) ||
                                   (r.Description != null && r.Description.ToLower().Contains(searchText)))
                        .OrderBy(r => r.Name)
                        .Select(r => new
                        {
                            r.RoomID,
                            r.Name,
                            Description = string.IsNullOrEmpty(r.Description) ? "Описание отсутствует" : r.Description,
                            ActiveSchedulesCount = context.Schedules
                                .Count(s => s.RoomID == r.RoomID &&
                                           (s.ScheduleStatus == "Активно" || s.ScheduleStatus == "Запланировано")),
                            IsInUse = context.Schedules
                                .Any(s => s.RoomID == r.RoomID &&
                                         (s.ScheduleStatus == "Активно" || s.ScheduleStatus == "Запланировано"))
                        })
                        .ToList();

                    RoomsDataGrid.ItemsSource = filteredRooms;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска: {ex.Message}");
            }
        }

        private void RoomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoomsDataGrid.SelectedItem != null)
            {
                var selectedRoom = (dynamic)RoomsDataGrid.SelectedItem;

                _editingRoomId = selectedRoom.RoomID;
                _isEditing = true;

                RoomNameTextBox.Text = selectedRoom.Name ?? "";
                RoomDescriptionTextBox.Text = selectedRoom.Description == "Описание отсутствует" ? "" : selectedRoom.Description ?? "";

                FormHeaderTextBlock.Text = "Редактировать комнату";
                AddButton.Visibility = Visibility.Collapsed;
                UpdateButton.Visibility = Visibility.Visible;

                HideValidationMessage();
            }
        }

        private void RoomsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(RoomNameTextBox.Text))
            {
                ShowValidationMessage("Пожалуйста, введите название комнаты.");
                RoomNameTextBox.Focus();
                return false;
            }

            if (RoomNameTextBox.Text.Trim().Length > 150)
            {
                ShowValidationMessage("Название комнаты не должно превышать 150 символов.");
                RoomNameTextBox.Focus();
                return false;
            }

            HideValidationMessage();
            return true;
        }

        private void ClearForm()
        {
            RoomNameTextBox.Clear();
            RoomDescriptionTextBox.Clear();
            _editingRoomId = null;
            _isEditing = false;
            FormHeaderTextBlock.Text = "Добавить новую комнату";
            AddButton.Visibility = Visibility.Visible;
            UpdateButton.Visibility = Visibility.Collapsed;
            RoomsDataGrid.SelectedItem = null;
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
