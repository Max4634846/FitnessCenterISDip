using FitnessCenterIS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FitnessCenterIS.View.Windows
{
    public partial class EditScheduleWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private Schedules _scheduleItem;
        private bool _isEditMode;
        private Dictionary<int, string> _scheduleColors;
        private ScheduleItem _scheduleItemWrapper;


        public EditScheduleWindow(BDFitnessClubDipEntities dbContext, Schedules item = null, Dictionary<int, string> scheduleColors = null)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _scheduleColors = scheduleColors ?? new Dictionary<int, string>();

            if (item != null)
            {
                _scheduleItem = item;
                _isEditMode = true;
                WindowTitle.Text = "Редактирование занятия";

                // Получаем цвет из словаря, если он существует
                string itemColor = _scheduleColors.ContainsKey(item.ScheduleID)
                    ? _scheduleColors[item.ScheduleID]
                    : "#3498db";

                // Создаем обертку ScheduleItem
                _scheduleItemWrapper = new ScheduleItem(item, itemColor);

                // Загружаем списки перед установкой значений
                LoadComboBoxes();

                // Устанавливаем DataContext для всего окна
                this.DataContext = _scheduleItem;

                // Напрямую устанавливаем значения
                TitleTextBox.Text = item.Title ?? "";

                // Устанавливаем значения даты и времени
                if (item.StartDateTime.HasValue)
                {
                    DatePicker.SelectedDate = item.StartDateTime.Value.Date;
                    StartTimeTextBox.Text = item.StartDateTime.Value.ToString("HH:mm");
                    EndTimeTextBox.Text = item.EndDateTime.Value.ToString("HH:mm");
                }

                // Отложенная установка выбранных значений
                this.Loaded += (s, e) => {
                    if (item.TrainerID.HasValue)
                        TrainerComboBox.SelectedValue = item.TrainerID.Value;

                    if (item.RoomID.HasValue)
                        RoomComboBox.SelectedValue = item.RoomID.Value;

                    if (item.SeasonticketServiceID.HasValue)
                        ServiceComboBox.SelectedValue = item.SeasonticketServiceID.Value;

                    if (item.ClientID.HasValue)
                        ClientComboBox.SelectedValue = item.ClientID.Value;

                    if (item.GroupID.HasValue)
                        GroupComboBox.SelectedValue = item.GroupID.Value;

                    // Установка статуса
                    if (!string.IsNullOrEmpty(item.ScheduleStatus))
                    {
                        switch (item.ScheduleStatus)
                        {
                            case "Активно": StatusComboBox.SelectedIndex = 0; break;
                            case "Отменено": StatusComboBox.SelectedIndex = 1; break;
                            case "Завершено": StatusComboBox.SelectedIndex = 2; break;
                            default: StatusComboBox.SelectedIndex = 0; break;
                        }
                    }
                    else
                    {
                        StatusComboBox.SelectedIndex = 0;
                    }
                };
            }
            else
            {
                // Код для нового элемента
                _scheduleItem = new Schedules
                {
                    StartDateTime = DateTime.Today.AddHours(9),
                    EndDateTime = DateTime.Today.AddHours(10),
                    ScheduleStatus = "Активно"
                };
                _isEditMode = false;
                WindowTitle.Text = "Новое занятие";

                // Создаем обертку для нового ScheduleItem с цветом по умолчанию
                _scheduleItemWrapper = new ScheduleItem(_scheduleItem);

                // Устанавливаем значения по умолчанию
                DatePicker.SelectedDate = DateTime.Today;
                StartTimeTextBox.Text = "09:00";
                EndTimeTextBox.Text = "10:00";

                // Загружаем выпадающие списки
                LoadComboBoxes();

                // По умолчанию - "Активно"
                StatusComboBox.SelectedIndex = 0;
            }

            // Устанавливаем DataContext для привязки данных
            this.DataContext = _scheduleItem;
        }




        private void LoadComboBoxes()
        {
            // Удалите код, связанный с цветами
            // Оставьте только загрузку комбобоксов для услуг, тренеров и помещений

            // Загрузка услуг
            ServiceComboBox.ItemsSource = _dbContext.Services.ToList();
            ServiceComboBox.DisplayMemberPath = "Name";
            ServiceComboBox.SelectedValuePath = "ServiceID";

            // Загрузка тренеров
            var staffData = _dbContext.Staffs
                .Where(s => s.Persons != null && s.Roles.Name == "Тренер")
                .Select(s => new {
                    s.StaffID,
                    Surname = s.Persons.Surname,
                    FirstName = s.Persons.Name,
                    MiddleName = s.Persons.MiddleName
                })
                .ToList()
                .Select(s => new {
                    StaffID = s.StaffID,
                    FullName = $"{s.Surname} {s.FirstName} {s.MiddleName}".Trim()
                })
                .ToList();

            TrainerComboBox.ItemsSource = staffData;
            TrainerComboBox.DisplayMemberPath = "FullName";
            TrainerComboBox.SelectedValuePath = "StaffID";

            // Загрузка помещений
            RoomComboBox.ItemsSource = _dbContext.Rooms.ToList();
            RoomComboBox.DisplayMemberPath = "Name";
            RoomComboBox.SelectedValuePath = "RoomID";

            // Загрузка клиентов
            var clientData = _dbContext.Clients
                .Where(c => c.Persons != null)
                .Select(c => new {
                    c.ClientID,
                    Surname = c.Persons.Surname,
                    FirstName = c.Persons.Name,
                    MiddleName = c.Persons.MiddleName
                })
                .ToList()
                .Select(c => new {
                    ClientID = c.ClientID,
                    FullName = $"{c.Surname} {c.FirstName} {c.MiddleName}".Trim()
                })
                .ToList();

            ClientComboBox.ItemsSource = clientData;
            ClientComboBox.DisplayMemberPath = "FullName";
            ClientComboBox.SelectedValuePath = "ClientID";

            // Загрузка групп
            GroupComboBox.ItemsSource = _dbContext.Groups.ToList();
            GroupComboBox.DisplayMemberPath = "Name";
            GroupComboBox.SelectedValuePath = "GroupID";

            // Статус расписания
            if (_scheduleItem.ScheduleStatus != null)
            {
                switch (_scheduleItem.ScheduleStatus)
                {
                    case "Активно":
                        StatusComboBox.SelectedIndex = 0;
                        break;
                    case "Отменено":
                        StatusComboBox.SelectedIndex = 1;
                        break;
                    case "Завершено":
                        StatusComboBox.SelectedIndex = 2;
                        break;
                    default:
                        StatusComboBox.SelectedIndex = 0;
                        break;
                }
            }
            else
            {
                StatusComboBox.SelectedIndex = 0;
            }
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateScheduleItem())
            {
                try
                {
                    // Обновляем заголовок
                    _scheduleItem.Title = TitleTextBox.Text;

                    // Обновляем время начала и окончания
                    DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                    // Парсим введенное время
                    if (TimeSpan.TryParse(StartTimeTextBox.Text, out TimeSpan startTime) &&
                        TimeSpan.TryParse(EndTimeTextBox.Text, out TimeSpan endTime))
                    {
                        _scheduleItem.StartDateTime = selectedDate.Date + startTime;
                        _scheduleItem.EndDateTime = selectedDate.Date + endTime;
                    }
                    else
                    {
                        MessageBox.Show("Пожалуйста, введите корректное время в формате ЧЧ:ММ", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Обновляем выбранные значения
                    _scheduleItem.SeasonticketServiceID = ServiceComboBox.SelectedValue != null ?
                        (int?)ServiceComboBox.SelectedValue : null;

                    _scheduleItem.TrainerID = TrainerComboBox.SelectedValue != null ?
                        (int?)TrainerComboBox.SelectedValue : null;

                    _scheduleItem.RoomID = RoomComboBox.SelectedValue != null ?
                        (int?)RoomComboBox.SelectedValue : null;

                    // Обновляем клиента и группу
                    _scheduleItem.ClientID = ClientComboBox.SelectedValue != null ?
                        (int?)ClientComboBox.SelectedValue : null;

                    _scheduleItem.GroupID = GroupComboBox.SelectedValue != null ?
                        (int?)GroupComboBox.SelectedValue : null;

                    if (CheckScheduleConflict())
                    {
                        // Если есть конфликт, добавляем клиента в список ожидания
                        AddToWaitingList();
                        MessageBox.Show("Выбранное время уже занято. Клиент добавлен в список ожидания.",
                            "Конфликт расписания", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                        return;
                    }

                    // Обновляем статус
                    if (StatusComboBox.SelectedItem is ComboBoxItem selectedStatus)
                    {
                        _scheduleItem.ScheduleStatus = selectedStatus.Content.ToString();
                    }

                    // Обновляем объект-обертку ScheduleItem
                    _scheduleItemWrapper.UpdateSchedule(_scheduleItem);

                    if (_isEditMode)
                    {
                        // Обновляем существующую запись
                        _dbContext.Entry(_scheduleItem).State = System.Data.Entity.EntityState.Modified;
                    }
                    else
                    {
                        // Добавляем новую запись
                        _dbContext.Schedules.Add(_scheduleItem);
                    }

                    _dbContext.SaveChanges();

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Метод для проверки конфликтов в расписании
        private bool CheckScheduleConflict()
        {
            if (_scheduleItem.SeasonticketServiceID.HasValue && _scheduleItem.StartDateTime.HasValue && _scheduleItem.EndDateTime.HasValue)
            {
                // Проверяем, есть ли другие активные занятия с той же услугой в то же время
                var conflictingSchedules = _dbContext.Schedules
                    .Where(s => s.ScheduleID != _scheduleItem.ScheduleID && // Не текущее занятие
                           s.SeasonticketServiceID == _scheduleItem.SeasonticketServiceID && // Та же услуга
                           s.ScheduleStatus == "Активно" && // Активный статус
                           ((s.StartDateTime <= _scheduleItem.StartDateTime && s.EndDateTime > _scheduleItem.StartDateTime) || // Начало внутри другого занятия
                            (s.StartDateTime < _scheduleItem.EndDateTime && s.EndDateTime >= _scheduleItem.EndDateTime) || // Конец внутри другого занятия
                            (s.StartDateTime >= _scheduleItem.StartDateTime && s.EndDateTime <= _scheduleItem.EndDateTime))) // Полностью внутри
                    .ToList();

                return conflictingSchedules.Any();
            }
            return false;
        }

        // Метод для добавления клиента в список ожидания
        private void AddToWaitingList()
        {
            if (_scheduleItem.ClientID.HasValue && _scheduleItem.SeasonticketServiceID.HasValue)
            {
                // Создаем запись в таблице WaitingLists
                var waitingList = new WaitingLists
                {
                    SheduleID = _scheduleItem.ScheduleID,
                    SeasonticketServiceID = _scheduleItem.SeasonticketServiceID,
                    DateTime = DateTime.Now,
                    IsActivite = true
                };

                _dbContext.WaitingLists.Add(waitingList);
                _dbContext.SaveChanges();

                // Создаем запись в таблице WaitingListClients
                var waitingListClient = new WaitingListClients
                {
                    WaitingListID = waitingList.WaitingListID,
                    ClientID = _scheduleItem.ClientID.Value,
                    EnrollmentDateTime = DateTime.Now,
                    IsProcessed = false,
                    Notes = $"Автоматически добавлен из-за конфликта расписания {_scheduleItem.StartDateTime:dd.MM.yyyy HH:mm}"
                };

                _dbContext.WaitingListClients.Add(waitingListClient);
                _dbContext.SaveChanges();
            }
        }




        private bool ValidateScheduleItem()
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Введите название занятия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (DatePicker.SelectedDate == null)
            {
                MessageBox.Show("Выберите дату", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeSpan.TryParse(StartTimeTextBox.Text, out TimeSpan startTime))
            {
                MessageBox.Show("Введите корректное время начала в формате ЧЧ:ММ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeSpan.TryParse(EndTimeTextBox.Text, out TimeSpan endTime))
            {
                MessageBox.Show("Введите корректное время окончания в формате ЧЧ:ММ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (startTime >= endTime)
            {
                MessageBox.Show("Время окончания должно быть позже времени начала", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        // Добавляем метод для удаления занятия
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_scheduleItem?.ScheduleID > 0)
            {
                var result = MessageBox.Show(
                    "Вы действительно хотите удалить это занятие?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {

                        // Удаляем запись из базы данных
                        _dbContext.Schedules.Remove(_scheduleItem);
                        _dbContext.SaveChanges();

                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

    }
}