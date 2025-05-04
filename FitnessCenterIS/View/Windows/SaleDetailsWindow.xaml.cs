using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Data.Entity;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Windows
{
    public partial class SaleDetailsWindow : Window
    {
        private BDFitnessClubDipEntities _context;
        private int _saleId;
        private Sales _sale;
        private ObservableCollection<PaymentViewModel> _payments;

        public SaleDetailsWindow(int saleId)
        {
            InitializeComponent();
            _context = new BDFitnessClubDipEntities();
            _saleId = saleId;
            _payments = new ObservableCollection<PaymentViewModel>();

            PaymentsDataGrid.ItemsSource = _payments;

            this.Loaded += (s, e) => LoadSaleDetails();
        }

        private void LoadSaleDetails()
        {
            try
            {
                // Загружаем данные о продаже
                _sale = _context.Sales
                    .Include(s => s.Seasontickets)
                    .Include(s => s.SeasonticketServices)
                    .Include(s => s.Vatrates)
                    .FirstOrDefault(s => s.SaleID == _saleId);

                if (_sale == null)
                {
                    MessageBox.Show("Продажа не найдена.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Заголовок окна
                TitleTextBlock.Text = $"Детали продажи №{_sale.SaleID}";


                SaleDateTimeTextBlock.Text = _sale.SaleDateTime?.ToString("dd.MM.yyyy HH:mm");
                StatusTextBlock.Text = _sale.StatusSale ?? "Неизвестен";

                // Информация об администраторе
                if (_sale.AdministratorID.HasValue)
                {
                    var administrator = _context.Users
                        .Include(u => u.Staffs.Persons)
                        .FirstOrDefault(u => u.UserID == _sale.AdministratorID);

                    if (administrator != null)
                    {
                        AdministratorTextBlock.Text = $"{administrator.Staffs.Persons.Surname} {administrator.Staffs.Persons.Name}";
                    }
                    else
                    {
                        AdministratorTextBlock.Text = "Неизвестен";
                    }
                }

                // Финансовая информация
                PriceTextBlock.Text = $"{_sale.PriceSold:N2} ₽";
                DiscountTextBlock.Text = $"{_sale.DiscountAmount:N2} ₽";
                VatTextBlock.Text = _sale.Vatrates?.Name ?? "Без НДС";

                // Способ оплаты - ищем в связанных платежах
                var payment = _context.Payments
                    .Include(p => p.PaymentMethods)
                    .FirstOrDefault(p => p.SaleID == _saleId);

                if (payment != null && payment.PaymentMethods != null)
                {
                    PaymentMethodTextBlock.Text = payment.PaymentMethods.Name;
                }
                else
                {
                    PaymentMethodTextBlock.Text = "Неизвестен";
                }

                // Информация о клиенте
                string clientName = "Не указан";
                string phoneNumber = "Неизвестен";
                string cardNumber = "Неизвестен";

                // Ищем клиента через абонемент
                if (_sale.SeasonticketID.HasValue)
                {
                    var seasonticketClient = _context.SeasonticketClients
                        .FirstOrDefault(sc => sc.SeasonticketID == _sale.SeasonticketID);

                    if (seasonticketClient != null)
                    {
                        var client = _context.Clients
                            .Include(c => c.Persons)
                            .FirstOrDefault(c => c.ClientID == seasonticketClient.ClientID);

                        if (client != null)
                        {
                            clientName = $"{client.Persons.Surname} {client.Persons.Name} {client.Persons.MiddleName}";
                            phoneNumber = client.Persons.PhoneNumber ?? "Не указан";
                            cardNumber = client.Persons.NumberCard ?? "Не указан";

                            // Бонусные баллы - предполагаем, что это 5% от стоимости
                            decimal bonusPoints = (_sale.PriceSold ?? 0) * 0.05m;
                            BonusPointsTextBlock.Text = $"{bonusPoints:N0}";
                        }
                    }
                }

                ClientNameTextBlock.Text = clientName;
                PhoneTextBlock.Text = phoneNumber;
                CardNumberTextBlock.Text = cardNumber;

                // Информация о продукте/услуге
                string productType = "Неизвестно";
                string productName = "Неизвестно";

                if (_sale.ClassificationID == 1)
                {
                    productType = "Абонемент";

                    if (_sale.Seasontickets != null)
                    {
                        productName = _sale.Seasontickets.Name;
                    }
                }
                else if (_sale.ClassificationID == 2)
                {
                    productType = "Услуга";

                    if (_sale.SeasonticketServices != null && _sale.SeasonticketServices.ServiceID.HasValue)
                    {
                        var service = _context.Services
                            .Find(_sale.SeasonticketServices.ServiceID.Value);

                        if (service != null)
                        {
                            productName = service.Name;
                        }
                    }
                }

                ProductTypeTextBlock.Text = productType;
                ProductNameTextBlock.Text = productName;

                // Информация о тренере
                if (_sale.TrainerID.HasValue)
                {
                    var trainer = _context.Staffs
                        .Include(s => s.Persons)
                        .FirstOrDefault(s => s.StaffID == _sale.TrainerID);

                    if (trainer != null)
                    {
                        TrainerTextBlock.Text = $"{trainer.Persons.Surname} {trainer.Persons.Name}";
                    }
                    else
                    {
                        TrainerTextBlock.Text = "Не назначен";
                    }
                }
                else
                {
                    TrainerTextBlock.Text = "Не назначен";
                }

                // Срок действия
                ValidityTextBlock.Text = $"{_sale.StartDateTime?.ToString("dd.MM.yyyy")} - {_sale.EndDateTime?.ToString("dd.MM.yyyy")}";

                // Остаток занятий
                RemainingVisitsTextBlock.Text = _sale.RemainingVisits?.ToString() ?? "Не указано";

                // Группа - если есть
                string groupName = "Не указана";

                if (_sale.SeasonticketServices != null)
                {
                    var groupMember = _context.GroupMembers
                        .Include(gm => gm.Groups)
                        .FirstOrDefault(gm => gm.SeasonticketServiceID == _sale.SeasonticketServices.SeasonticketServiceID);

                    if (groupMember != null && groupMember.Groups != null)
                    {
                        groupName = groupMember.Groups.Name;
                    }
                }

                GroupTextBlock.Text = groupName;

                // Загружаем историю платежей
                _payments.Clear();

                var payments = _context.Payments
                    .Include(p => p.PaymentMethods)
                    .Where(p => p.SaleID == _saleId)
                    .ToList();

                foreach (var p in payments)
                {
                    _payments.Add(new PaymentViewModel
                    {
                        PaymentID = p.PaymentID,
                        DateTime = p.DateTime,
                        Amount = p.Amount ?? 0,
                        PaymentMethod = p.PaymentMethods?.Name ?? "Неизвестен"
                    });
                }

                // Примечания
                NotesTextBox.Text = "Примечаний нет.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных продажи: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statusSelectionWindow = new StatusSelectionWindow(_sale.StatusSale);

                if (statusSelectionWindow.ShowDialog() == true)
                {
                    string newStatus = statusSelectionWindow.SelectedStatus;

                    if (_sale != null)
                    {
                        _sale.StatusSale = newStatus;
                        _context.SaveChanges();

                        // Обновляем отображение
                        StatusTextBlock.Text = newStatus;

                        MessageBox.Show($"Статус продажи изменен на '{newStatus}'",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении статуса: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем данные о продаже
                var sale = _context.Sales.Find(_saleId);
                if (sale == null)
                {
                    MessageBox.Show("Продажа не найдена.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Получаем данные о платеже
                var payment = _context.Payments
                    .Include(p => p.PaymentMethods)
                    .FirstOrDefault(p => p.SaleID == _saleId);

                if (payment == null)
                {
                    // Если данных о платеже нет, создаем сводный объект с имеющимися данными
                    var paymentResult = new PaymentResult
                    {
                        Success = true,
                        CardAmount = sale.PriceSold ?? 0,
                        DepositAmount = 0,
                        BonusAmount = 0,
                        PaymentMethodId = 1 // Предполагаем, что 1 - ID для наличных/карты
                    };

                    // Показываем предварительный просмотр чека
                    ReceiptPrinter.ShowReceiptPreview(sale, paymentResult);
                }
                else
                {
                    // Создаем объект с данными о платеже
                    var paymentResult = new PaymentResult
                    {
                        Success = true,
                        CardAmount = payment.Amount ?? 0,
                        DepositAmount = 0,
                        BonusAmount = (sale.PriceSold ?? 0) - (payment.Amount ?? 0),
                        PaymentMethodId = payment.PaymentMethodID ?? 1
                    };

                    // Показываем предварительный просмотр чека
                    ReceiptPrinter.ShowReceiptPreview(sale, paymentResult);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати чека: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class PaymentViewModel
    {
        public int PaymentID { get; set; }
        public DateTime? DateTime { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
    }
}