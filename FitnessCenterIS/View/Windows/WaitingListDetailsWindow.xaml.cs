using FitnessCenterIS.Model;
using System;
using System.Windows;
using System.Windows.Data;

namespace FitnessCenterIS.View.Windows
{
    public partial class WaitingListDetailsWindow : Window
    {
        private readonly WaitingListItem _waitingListItem;
        private readonly BDFitnessClubDipEntities _dbContext;

        // Свойство для передачи информации о заголовке занятия
        public string ScheduleTitle { get; set; }

        public WaitingListDetailsWindow(WaitingListItem waitingListItem, BDFitnessClubDipEntities dbContext)
        {
            InitializeComponent();
            _waitingListItem = waitingListItem;
            _dbContext = dbContext;

            // Получаем заголовок занятия, если он доступен
            if (_waitingListItem.WaitingListClient.WaitingLists?.Schedules != null)
            {
                ScheduleTitle = _waitingListItem.WaitingListClient.WaitingLists.Schedules.Title;
            }
            else
            {
                ScheduleTitle = "Нет информации";
            }

            // Устанавливаем контекст данных
            this.DataContext = _waitingListItem;
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем связанное расписание
                var scheduleId = _waitingListItem.WaitingListClient.WaitingLists.SheduleID;
                var schedule = _dbContext.Schedules.Find(scheduleId);

                if (schedule != null)
                {
                    // Проверяем, доступно ли занятие сейчас
                    if (schedule.ClientID != null)
                    {
                        MessageBox.Show("Данное занятие уже занято другим клиентом.",
                                       "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Обновляем занятие с клиентом из списка ожидания
                    schedule.ClientID = _waitingListItem.ClientID;
                    _dbContext.Entry(schedule).State = System.Data.Entity.EntityState.Modified;

                    // Отмечаем запись в списке ожидания как обработанную
                    _waitingListItem.WaitingListClient.IsProcessed = true;
                    _waitingListItem.WaitingListClient.Notes += $"\nОбработано {DateTime.Now:dd.MM.yyyy HH:mm}";
                    _dbContext.Entry(_waitingListItem.WaitingListClient).State = System.Data.Entity.EntityState.Modified;

                    _dbContext.SaveChanges();
                    MessageBox.Show("Клиент успешно добавлен в расписание!",
                                   "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем элементы интерфейса
                    this.DataContext = null;
                    this.DataContext = _waitingListItem;
                }
                else
                {
                    MessageBox.Show("Занятие не найдено. Возможно, оно было удалено.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке записи: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите отклонить этот запрос?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Отмечаем запись как обработанную с примечанием об отклонении
                    _waitingListItem.WaitingListClient.IsProcessed = true;
                    _waitingListItem.WaitingListClient.Notes += $"\nОтклонено {DateTime.Now:dd.MM.yyyy HH:mm}";
                    _dbContext.Entry(_waitingListItem.WaitingListClient).State = System.Data.Entity.EntityState.Modified;

                    _dbContext.SaveChanges();
                    MessageBox.Show("Запрос отклонен.",
                                   "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем элементы интерфейса
                    this.DataContext = null;
                    this.DataContext = _waitingListItem;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при отклонении запроса: {ex.Message}",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}