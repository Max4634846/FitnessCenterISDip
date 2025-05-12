using FitnessCenterIS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FitnessCenterIS.View.Windows
{
    public partial class WaitingListWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private List<WaitingListItem> _waitingListItems;


        public WaitingListWindow(BDFitnessClubDipEntities dbContext = null)
        {
            InitializeComponent();
            // Если контекст не передан, создаём новый
            _dbContext = dbContext ?? new BDFitnessClubDipEntities();

            // Устанавливаем стандартный фильтр в ComboBox
            FilterComboBox.SelectedIndex = 0;

            // Теперь загружаем данные
            LoadWaitingList();
        }

        private void LoadWaitingList()
        {
            try
            {
                // Определяем текущий фильтр - проверка на null
                string filterOption = "Все записи";
                if (FilterComboBox != null && FilterComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    filterOption = selectedItem.Content.ToString();
                }

                // Загружаем данные из базы с включенными связями
                var waitingListClients = _dbContext.WaitingListClients
                    .Include("Clients.Persons")
                    .Include("WaitingLists.Schedules")
                    .Include("WaitingLists.SeasonticketServices.Services")
                    .ToList();

                // Применяем фильтр, если он выбран
                if (filterOption == "Активные")
                {
                    waitingListClients = waitingListClients.Where(w => !w.IsProcessed.HasValue || !w.IsProcessed.Value).ToList();
                }
                else if (filterOption == "Обработанные")
                {
                    waitingListClients = waitingListClients.Where(w => w.IsProcessed.HasValue && w.IsProcessed.Value).ToList();
                }

                // Создаем список элементов-оберток
                _waitingListItems = waitingListClients
                    .Select(w => new WaitingListItem(w))
                    .OrderByDescending(w => w.EnrollmentDateTime)
                    .ToList();

                // Привязываем данные к DataGrid
                WaitingListDataGrid.ItemsSource = _waitingListItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка ожидания: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadWaitingList();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadWaitingList();
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = WaitingListDataGrid.SelectedItem as WaitingListItem;
            if (selectedItem != null && !selectedItem.IsProcessed)
            {
                try
                {
                    // Получаем связанное расписание
                    var scheduleId = selectedItem.WaitingListClient.WaitingLists.SheduleID;
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
                        schedule.ClientID = selectedItem.ClientID;
                        _dbContext.Entry(schedule).State = System.Data.Entity.EntityState.Modified;

                        // Отмечаем запись в списке ожидания как обработанную
                        selectedItem.WaitingListClient.IsProcessed = true;
                        selectedItem.WaitingListClient.Notes += $"\nОбработано {DateTime.Now:dd.MM.yyyy HH:mm}";
                        _dbContext.Entry(selectedItem.WaitingListClient).State = System.Data.Entity.EntityState.Modified;

                        _dbContext.SaveChanges();
                        MessageBox.Show("Клиент успешно добавлен в расписание!",
                                       "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadWaitingList();
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
            else
            {
                MessageBox.Show("Выберите активную запись из списка ожидания.",
                               "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = WaitingListDataGrid.SelectedItem as WaitingListItem;
            if (selectedItem != null)
            {
                var result = MessageBox.Show(
                    "Вы действительно хотите удалить эту запись из списка ожидания?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _dbContext.WaitingListClients.Remove(selectedItem.WaitingListClient);
                        _dbContext.SaveChanges();
                        LoadWaitingList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении записи: {ex.Message}",
                                       "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите запись для удаления.",
                               "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void WaitingListDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = WaitingListDataGrid.SelectedItem as WaitingListItem;
            if (selectedItem != null)
            {
                // Открываем детальную информацию о записи в списке ожидания
                var details = new WaitingListDetailsWindow(selectedItem, _dbContext);
                details.ShowDialog();
                LoadWaitingList(); // Обновляем список после закрытия
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Конвертер для отображения статуса
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isProcessed)
            {
                return isProcessed ? "Обработано" : "Ожидание";
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}