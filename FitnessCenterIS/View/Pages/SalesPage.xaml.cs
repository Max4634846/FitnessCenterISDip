using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data.Entity;
using FitnessCenterIS.Model;
using System.IO;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Media;

namespace FitnessCenterIS.View.Pages
{
    public partial class SalesPage : Page
    {
        private BDFitnessClubDipEntities _context;
        private ObservableCollection<SaleViewModel> _sales;
        private CollectionViewSource _salesViewSource;
        private SaleViewModel _selectedSale;

        public SalesPage()
        {
            InitializeComponent();
            _context = new BDFitnessClubDipEntities();
            _sales = new ObservableCollection<SaleViewModel>();

            _salesViewSource = new CollectionViewSource();
            _salesViewSource.Source = _sales;

            SalesDataGrid.ItemsSource = _salesViewSource.View;

            // Устанавливаем выбранные значения в комбобоксах
            SaleTypeComboBox.SelectedIndex = 0; // "Все продажи"
            StatusComboBox.SelectedIndex = 0; // "Все статусы"

            // Загружаем продажи при загрузке страницы
            this.Loaded += (s, e) => LoadSales();
        }

        private void LoadSales()
        {
            try
            {


                // Подготавливаем базовый запрос с явным упорядочиванием
                var salesQuery = _context.Sales
                    .OrderByDescending(s => s.SaleDateTime);

                // Применяем фильтры, сохраняя упорядочивание
                IQueryable<Sales> filteredQuery = salesQuery;

                // Применяем фильтр по типу продажи
                if (SaleTypeComboBox.SelectedIndex == 1) // Абонементы
                {
                    filteredQuery = filteredQuery.Where(s => s.ClassificationID == 1);
                }
                else if (SaleTypeComboBox.SelectedIndex == 2) // Услуги
                {
                    filteredQuery = filteredQuery.Where(s => s.ClassificationID == 2);
                }

                // Применяем фильтр по статусу
                if (StatusComboBox.SelectedIndex > 0) // Не "Все статусы"
                {
                    string statusFilter = ((ComboBoxItem)StatusComboBox.SelectedItem).Content.ToString();
                    filteredQuery = filteredQuery.Where(s => s.StatusSale == statusFilter);
                }

                // Загружаем отфильтрованные продажи
                var filteredSales = filteredQuery.ToList();

                // Очищаем коллекцию и заполняем новыми данными
                _sales.Clear();

                decimal totalAmount = 0;

                foreach (var sale in filteredSales)
                {
                    string clientName = "Не указан";

                    // Пытаемся найти клиента через абонемент
                    if (sale.SeasonticketID.HasValue)
                    {
                        var seasonticketClient = _context.SeasonticketClients
                            .FirstOrDefault(sc => sc.SeasonticketID == sale.SeasonticketID);

                        if (seasonticketClient != null)
                        {
                            var client = _context.Clients
                                .Include(c => c.Persons)
                                .FirstOrDefault(c => c.ClientID == seasonticketClient.ClientID);

                            if (client != null)
                            {
                                clientName = $"{client.Persons.Surname} {client.Persons.Name} {client.Persons.MiddleName}";
                            }
                        }
                    }

                    // Определяем название услуги или абонемента
                    string serviceName = "Неизвестно";
                    if (sale.SeasonticketID.HasValue)
                    {
                        var seasonticket = _context.Seasontickets
                            .Find(sale.SeasonticketID.Value);
                        if (seasonticket != null)
                        {
                            serviceName = $"Абонемент: {seasonticket.Name}";
                        }
                    }
                    else if (sale.SeasonticketServiceID.HasValue)
                    {
                        var seasonticketService = _context.SeasonticketServices
                            .Find(sale.SeasonticketServiceID.Value);
                        if (seasonticketService != null && seasonticketService.ServiceID.HasValue)
                        {
                            var service = _context.Services
                                .Find(seasonticketService.ServiceID.Value);
                            if (service != null)
                            {
                                serviceName = $"Услуга: {service.Name}";
                            }
                        }
                    }

                    // Получаем имя тренера, если есть
                    string trainerName = "Не назначен";
                    if (sale.TrainerID.HasValue)
                    {
                        var trainer = _context.Staffs
                            .Include(s => s.Persons)
                            .FirstOrDefault(s => s.StaffID == sale.TrainerID);

                        if (trainer != null)
                        {
                            trainerName = $"{trainer.Persons.Surname} {trainer.Persons.Name}";
                        }
                    }

                    // Получаем имя администратора
                    string administratorName = "Неизвестно";
                    if (sale.AdministratorID.HasValue)
                    {
                        var administrator = _context.Users
                            .Include(u => u.Staffs.Persons)
                            .FirstOrDefault(u => u.UserID == sale.AdministratorID);

                        if (administrator != null)
                        {
                            administratorName = $"{administrator.Staffs.Persons.Surname} {administrator.Staffs.Persons.Name}";
                        }
                    }

                    // Добавляем в модель представления
                    _sales.Add(new SaleViewModel
                    {
                        SaleID = sale.SaleID,
                        SaleDateTime = sale.SaleDateTime,
                        ClientName = clientName,
                        ServiceName = serviceName,
                        PriceSold = sale.PriceSold ?? 0,
                        StatusSale = sale.StatusSale,
                        TrainerName = trainerName,
                        RemainingVisits = sale.RemainingVisits ?? 0,
                        AdministratorName = administratorName
                    });

                    // Суммируем для статистики
                    totalAmount += sale.PriceSold ?? 0;
                }

                // Обновляем статистику
                TotalSalesTextBlock.Text = $"Всего продаж: {_sales.Count}";
                TotalAmountTextBlock.Text = $"Общая сумма: {totalAmount:N2} ₽";

                ApplyTextFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке продаж: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyTextFilter()
        {
            string searchText = SearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _salesViewSource.View.Filter = null;
                return;
            }

            _salesViewSource.View.Filter = item =>
            {
                if (item is SaleViewModel sale)
                {
                    return sale.ClientName.ToLower().Contains(searchText) ||
                           sale.ServiceName.ToLower().Contains(searchText) ||
                           sale.TrainerName.ToLower().Contains(searchText) ||
                           sale.AdministratorName.ToLower().Contains(searchText) ||
                           sale.StatusSale.ToLower().Contains(searchText) ||
                           sale.SaleID.ToString().Contains(searchText);
                }
                return false;
            };
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyTextFilter();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSales();
        }

        private void SaleTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                LoadSales();
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                LoadSales();
        }

        private void SalesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSale = SalesDataGrid.SelectedItem as SaleViewModel;
        }

        private void SalesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedSale != null)
            {
                ViewSaleDetails();
            }
        }

        private void SalesDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var sale = e.Row.Item as SaleViewModel;
            if (sale != null)
            {
                // Раскрашиваем строки в зависимости от статуса
                switch (sale.StatusSale)
                {
                    case "Активна":
                        e.Row.Background = new SolidColorBrush(Colors.White);
                        break;
                    case "Завершена":
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(240, 255, 240)); // Светло-зеленый
                        break;
                    case "Заморожена":
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(235, 245, 251)); // Светло-голубой
                        break;
                    case "Отменена":
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 240, 240)); // Светло-красный
                        break;
                    default:
                        e.Row.Background = new SolidColorBrush(Colors.White);
                        break;
                }
            }
        }

        private void ViewSaleDetails()
        {
            var saleDetailsWindow = new Windows.SaleDetailsWindow(_selectedSale.SaleID);
            saleDetailsWindow.ShowDialog();

            // После закрытия окна обновляем данные на случай изменений
            LoadSales();
        }

        private void ChangeSaleStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSale != null)
            {
                var statusSelectionWindow = new Windows.StatusSelectionWindow(_selectedSale.StatusSale);

                if (statusSelectionWindow.ShowDialog() == true)
                {
                    try
                    {
                        string newStatus = statusSelectionWindow.SelectedStatus;
                        var sale = _context.Sales.Find(_selectedSale.SaleID);

                        if (sale != null)
                        {
                            sale.StatusSale = newStatus;
                            _context.SaveChanges();

                            MessageBox.Show($"Статус продажи изменен на '{newStatus}'",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                            LoadSales();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при изменении статуса: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите продажу для изменения статуса",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSale != null)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Вы действительно хотите удалить продажу №{_selectedSale.SaleID}?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var sale = _context.Sales.Find(_selectedSale.SaleID);

                        if (sale != null)
                        {
                            // Удаляем связанные платежи
                            var payments = _context.Payments.Where(p => p.SaleID == sale.SaleID).ToList();
                            foreach (var payment in payments)
                            {
                                _context.Payments.Remove(payment);
                            }

                            // Удаляем связи с группами
                            if (sale.SeasonticketServiceID.HasValue)
                            {
                                var groupMembers = _context.GroupMembers
                                    .Where(gm => gm.SeasonticketServiceID == sale.SeasonticketServiceID)
                                    .ToList();

                                foreach (var member in groupMembers)
                                {
                                    _context.GroupMembers.Remove(member);
                                }
                            }

                            // Удаляем связи с абонементами
                            var seasonticketSales = _context.SeasonticketSales
                                .Where(ss => ss.SaleID == sale.SaleID)
                                .ToList();

                            foreach (var seasonticketSale in seasonticketSales)
                            {
                                _context.SeasonticketSales.Remove(seasonticketSale);
                            }

                            // Удаляем продажу
                            _context.Sales.Remove(sale);
                            _context.SaveChanges();

                            MessageBox.Show("Продажа успешно удалена",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                            LoadSales();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении продажи: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите продажу для удаления",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем диалог сохранения файла
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Excel файлы (*.xlsx)|*.xlsx",
                    FileName = $"Отчет_по_продажам_{DateTime.Now:yyyy-MM-dd}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportToExcel(saveDialog.FileName);

                    MessageBox.Show($"Отчет успешно сохранен в файл:\n{saveDialog.FileName}",
                        "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string filePath)
        {
            // Простой экспорт в CSV
            using (StreamWriter writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // Заголовок
                writer.WriteLine("№;Дата продажи;Клиент;Товар/Услуга;Сумма;Статус;Тренер;Осталось посещений;Администратор");

                // Данные
                foreach (var sale in _sales)
                {
                    writer.WriteLine(
                        $"{sale.SaleID};" +
                        $"{sale.SaleDateTime?.ToString("dd.MM.yyyy HH:mm")};" +
                        $"{sale.ClientName};" +
                        $"{sale.ServiceName};" +
                        $"{sale.PriceSold:F2};" +
                        $"{sale.StatusSale};" +
                        $"{sale.TrainerName};" +
                        $"{sale.RemainingVisits};" +
                        $"{sale.AdministratorName}");
                }
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно выбора клиента
            var clientSelectionWindow = new Windows.ClientSelectionWindow();

            if (clientSelectionWindow.ShowDialog() == true)
            {
                int clientId = clientSelectionWindow.SelectedClientId;
                var newSaleWindow = new Windows.NewSaleWindow(clientId);

                if (newSaleWindow.ShowDialog() == true)
                {
                    // После успешной продажи обновляем список
                    LoadSales();

                    // Прокручиваем к последней (новой) продаже
                    if (SalesDataGrid.Items.Count > 0)
                    {
                        SalesDataGrid.SelectedIndex = 0;
                        SalesDataGrid.ScrollIntoView(SalesDataGrid.SelectedItem);
                    }
                }
            }
        }
    }

    public class SaleViewModel
    {
        public int SaleID { get; set; }
        public DateTime? SaleDateTime { get; set; }
        public string ClientName { get; set; }
        public string ServiceName { get; set; }
        public decimal PriceSold { get; set; }
        public string StatusSale { get; set; }
        public string TrainerName { get; set; }
        public int RemainingVisits { get; set; }
        public string AdministratorName { get; set; }
    }
}