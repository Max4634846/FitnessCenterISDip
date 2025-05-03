using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using FitnessCenterIS.Model;
using static FitnessCenterIS.View.Windows.TaskWindow;

namespace FitnessCenterIS.View.Windows
{
    public partial class NewSaleWindow : Window
    {
        private BDFitnessClubDipEntities _context;
        private ObservableCollection<ClientInfo> _allClients;
        private ClientInfo _selectedClient;
        private decimal _basePrice;
        private ObservableCollection<SelectedMembership> _selectedMemberships;
        private decimal _totalBonusPoints;
        private int _clientId;



        public NewSaleWindow(int clientId)
        {
            InitializeComponent();
            _context = new BDFitnessClubDipEntities();
            _selectedMemberships = new ObservableCollection<SelectedMembership>();
            _totalBonusPoints = 0;

            this.Loaded += (s, e) =>
            {
                LoadData();
                InitializeUI();
                SetClientInfo(clientId);
            };
        }


        private void SetClientInfo(int clientId)
        {
            var clientData = _context.Clients.Include(c => c.Persons).FirstOrDefault(c => c.ClientID == clientId);

            if (clientData != null)
            {
                _selectedClient = new ClientInfo
                {
                    ClientID = clientData.ClientID,
                    FullName = $"{clientData.Persons.Surname} {clientData.Persons.Name} {clientData.Persons.MiddleName}",
                    CardNumber = clientData.Persons.NumberCard,
                    BonusPoints = clientData.BonuseBalance ?? 0
                };

                ClientTextBox.Text = _selectedClient.ToString();
                ClientInfoTextBlock.Text =
                    $"ФИО: {_selectedClient.FullName}\nНомер карты: {_selectedClient.CardNumber}\nБонусные баллы: {_selectedClient.BonusPoints}";
                CheckForActiveMembership(clientId);
                ClientsPopup.IsOpen = false;
                SaveSaleButton.IsEnabled = true;
            }
        }

        private void InitializeUI()
        {
            // Проверка на null для избежания ошибок
            if (MembershipLabel == null || ServiceLabel == null || TrainerLabel == null ||
                RemainingVisitsLabel == null || RemainingVisitsComboBox == null ||
                StartDatePanel == null || StartDateTimePicker == null ||
                EndDatePanel == null || EndDateTimePicker == null ||
                SelectedMembershipsPanel == null || SelectedMembershipsListView == null ||
                RemoveMembershipButton == null)
                return;

            // Настройка видимости элементов для абонемента
            MembershipSelectionPanel.Visibility = Visibility.Visible;
            ServiceSelectionPanel.Visibility = Visibility.Collapsed;
            TrainerLabel.Visibility = Visibility.Visible;
            TrainerComboBox.Visibility = Visibility.Visible;
            RemainingVisitsLabel.Visibility = Visibility.Visible;
            RemainingVisitsComboBox.Visibility = Visibility.Visible;
            StartDatePanel.Visibility = Visibility.Visible;
            EndDatePanel.Visibility = Visibility.Visible;
            SelectedMembershipsPanel.Visibility = Visibility.Visible;
            RemoveMembershipButton.Visibility = Visibility.Visible;

            // Установка значений по умолчанию
            SaleDatePicker.SelectedDate = DateTime.Now;
            StartDateTimePicker.SelectedDate = DateTime.Now;
            EndDateTimePicker.SelectedDate = DateTime.Now.AddMonths(1);

            StatusSaleComboBox.ItemsSource = new List<string> { "Активна", "Завершена", "Отменена" };
            StatusSaleComboBox.SelectedItem = "Активна";

            RemainingVisitsComboBox.SelectedIndex = 0;

            SelectedMembershipsListView.ItemsSource = _selectedMemberships;

            // Установка значения бонусных баллов
            BonusPointsTextBlock.Text = "0";
        }

        private void LoadData()
        {
            // Загрузка клиентов
            var clientsData = _context.Clients
                .Include(c => c.Persons)
                .OrderBy(c => c.Persons.Surname)
                .ThenBy(c => c.Persons.Name)
                .ToList();

            _allClients = new ObservableCollection<ClientInfo>(
                clientsData.Select(c => new ClientInfo
                {
                    ClientID = c.ClientID,
                    FullName = $"{c.Persons.Surname} {c.Persons.Name} {c.Persons.MiddleName}",
                    CardNumber = c.Persons.NumberCard,
                    BonusPoints = c.BonuseBalance ?? 0
                }));

            // Загрузка администраторов
            var adminsData = _context.Users
                .Include(u => u.Staffs.Persons)
                .Where(u => u.Staffs.RoleID == 1)
                .ToList();

            var administrators = adminsData.Select(u => new StaffsCollection
            {
                StaffID = u.UserID,
                Name = $"{u.Staffs.Persons.Surname} {u.Staffs.Persons.Name}"
            }).ToList();

            // Загрузка тренеров
            var trainersData = _context.Staffs
                .Include(s => s.Persons)
                .Where(s => s.RoleID == 2)
                .ToList();

            var trainers = trainersData.Select(s => new ServiceTrainerCollection
            {
                TrainerID = s.StaffID,
                Name = $"{s.Persons.Surname} {s.Persons.Name}"
            }).ToList();

            var memberships = _context.Seasontickets.ToList();
            var services = _context.Services.ToList();
            var vatRates = _context.Vatrates.ToList();
            var paymentMethods = _context.PaymentMethods.ToList();

            AdministratorComboBox.ItemsSource = administrators;
            AdministratorComboBox.DisplayMemberPath = "Name";
            AdministratorComboBox.SelectedValuePath = "StaffID";

            TrainerComboBox.ItemsSource = trainers;
            TrainerComboBox.DisplayMemberPath = "Name";
            TrainerComboBox.SelectedValuePath = "TrainerID";

            MembershipComboBox.ItemsSource = memberships;
            MembershipComboBox.DisplayMemberPath = "Name";
            MembershipComboBox.SelectedValuePath = "SeasonticketID";

            ServiceComboBox.ItemsSource = services;
            ServiceComboBox.DisplayMemberPath = "Name";
            ServiceComboBox.SelectedValuePath = "ServiceID";

            VatRateComboBox.ItemsSource = vatRates;
            VatRateComboBox.DisplayMemberPath = "Name";
            VatRateComboBox.SelectedValuePath = "VatRateID";

            PaymentMethodComboBox.ItemsSource = paymentMethods;
            PaymentMethodComboBox.DisplayMemberPath = "Name";
            PaymentMethodComboBox.SelectedValuePath = "PaymentMethodID";
        }

        private void ClientTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ClientTextBox == null || ClientsPopup == null || ClientsListBoxInPopup == null)
                return;

            string searchText = ClientTextBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ClientsPopup.IsOpen = false;
                return;
            }

            var filteredClients = _allClients.Where(c =>
                c.FullName.ToLower().Contains(searchText) ||
                c.CardNumber.ToLower().Contains(searchText)).ToList();

            ClientsListBoxInPopup.ItemsSource = new ObservableCollection<ClientInfo>(filteredClients);
            if (filteredClients.Any())
            {
                ClientsPopup.IsOpen = true;
            }
            else
            {
                ClientsPopup.IsOpen = false;
            }
        }

        private void ClientsListBoxInPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientsListBoxInPopup == null || ClientTextBox == null ||
                ClientInfoTextBlock == null || ClientsPopup == null)
                return;

            if (ClientsListBoxInPopup.SelectedItem is ClientInfo selectedClient)
            {
                _selectedClient = selectedClient;
                ClientTextBox.Text = selectedClient.ToString();
                ClientInfoTextBlock.Text = $"ФИО: {selectedClient.FullName}\nНомер карты: {selectedClient.CardNumber}\nБонусные баллы: {selectedClient.BonusPoints}";
                ClientsPopup.IsOpen = false;

                // Проверка на наличие действующего абонемента
                CheckForActiveMembership(selectedClient.ClientID);
            }
        }

        private void CheckForActiveMembership(int clientId)
        {
            if (SaveSaleButton == null)
                return;

            // Проверяем наличие активных абонементов у клиента через связанные таблицы
            bool hasActiveMembership = _context.Sales
                .Any(s => s.Seasontickets.SeasonticketClients.Any(sc => sc.ClientID == clientId)
                     && s.RemainingVisits > 0
                     && s.StatusSale == "Активна");

            if (hasActiveMembership && MembershipRadioButton.IsChecked == true)
            {
                MessageBox.Show("Клиент уже имеет действующий абонемент. Выдача нового абонемента возможна только после окончания действия текущего абонемента.",
                                "Действующий абонемент", MessageBoxButton.OK, MessageBoxImage.Warning);
                SaveSaleButton.IsEnabled = false;
            }
            else
            {
                SaveSaleButton.IsEnabled = true;
            }
        }

        private void SaleTypeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MembershipRadioButton == null || ServiceRadioButton == null ||
                MembershipSelectionPanel == null || ServiceSelectionPanel == null ||
                TrainerLabel == null || TrainerComboBox == null ||
                RemainingVisitsLabel == null || RemainingVisitsComboBox == null ||
                StartDatePanel == null || EndDatePanel == null ||
                PriceSoldTextBox == null || SaveSaleButton == null ||
                SelectedMembershipsPanel == null || RemoveMembershipButton == null)
                return;

            if (MembershipRadioButton.IsChecked == true)
            {
                MembershipSelectionPanel.Visibility = Visibility.Visible;
                ServiceSelectionPanel.Visibility = Visibility.Collapsed;
                TrainerLabel.Visibility = Visibility.Visible;
                TrainerComboBox.Visibility = Visibility.Visible;
                RemainingVisitsLabel.Visibility = Visibility.Visible;
                RemainingVisitsComboBox.Visibility = Visibility.Visible;
                StartDatePanel.Visibility = Visibility.Visible;
                EndDatePanel.Visibility = Visibility.Visible;
                SelectedMembershipsPanel.Visibility = Visibility.Visible;
                RemoveMembershipButton.Visibility = Visibility.Visible;
                PriceSoldTextBox.IsEnabled = false;

                if (_selectedClient != null)
                {
                    CheckForActiveMembership(_selectedClient.ClientID);
                }
            }
            else if (ServiceRadioButton.IsChecked == true)
            {
                MembershipSelectionPanel.Visibility = Visibility.Collapsed;
                ServiceSelectionPanel.Visibility = Visibility.Visible;
                TrainerLabel.Visibility = Visibility.Visible;
                TrainerComboBox.Visibility = Visibility.Visible;
                RemainingVisitsLabel.Visibility = Visibility.Collapsed;
                RemainingVisitsComboBox.Visibility = Visibility.Collapsed;
                StartDatePanel.Visibility = Visibility.Collapsed;
                EndDatePanel.Visibility = Visibility.Collapsed;
                SelectedMembershipsPanel.Visibility = Visibility.Collapsed;
                RemoveMembershipButton.Visibility = Visibility.Collapsed;
                PriceSoldTextBox.IsEnabled = true;
                SaveSaleButton.IsEnabled = true;
            }
        }

        private void MembershipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MembershipComboBox == null || PriceSoldTextBox == null)
                return;

            if (MembershipComboBox.SelectedItem is Seasontickets selectedMembership)
            {
                _basePrice = selectedMembership.Price.GetValueOrDefault();
                PriceSoldTextBox.Text = _basePrice.ToString();
                UpdateTotalCost();
            }
            else
            {
                PriceSoldTextBox.Text = "";
            }
        }

        private void ServiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServiceComboBox == null || PriceSoldTextBox == null)
                return;

            if (ServiceComboBox.SelectedItem is Services selectedService)
            {
                _basePrice = selectedService.Price.GetValueOrDefault();
                PriceSoldTextBox.Text = _basePrice.ToString();
            }
            else
            {
                PriceSoldTextBox.Text = "";
            }
        }

        private void AddMembership_Click(object sender, RoutedEventArgs e)
        {
            if (MembershipComboBox == null || RemainingVisitsComboBox == null ||
                SelectedMembershipsListView == null)
                return;

            if (MembershipComboBox.SelectedItem is Seasontickets selectedMembership &&
                RemainingVisitsComboBox.SelectedItem is ComboBoxItem selectedVisitsItem)
            {
                int visits = int.Parse(selectedVisitsItem.Content.ToString());

                // Проверяем, не добавлен ли уже этот абонемент
                bool alreadyAdded = _selectedMemberships.Any(m => m.SeasonticketID == selectedMembership.SeasonticketID);
                if (alreadyAdded)
                {
                    MessageBox.Show("Этот абонемент уже добавлен в список.", "Предупреждение",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newMembership = new SelectedMembership
                {
                    SeasonticketID = selectedMembership.SeasonticketID,
                    Name = selectedMembership.Name,
                    Price = selectedMembership.Price.GetValueOrDefault(),
                    Visits = visits
                };

                _selectedMemberships.Add(newMembership);

                // Рассчитываем бонусные баллы
                decimal bonusPoints = CalculateBonusPoints(selectedMembership, visits);
                _totalBonusPoints += bonusPoints;

                UpdateTotalCost();
                UpdateBonusPoints();
            }
        }

        private void RemoveMembership_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMembershipsListView == null)
                return;

            if (SelectedMembershipsListView.SelectedItem is SelectedMembership selectedMembership)
            {
                // Вычитаем бонусные баллы
                var membership = _context.Seasontickets.Find(selectedMembership.SeasonticketID);
                if (membership != null)
                {
                    decimal bonusPoints = CalculateBonusPoints(membership, selectedMembership.Visits);
                    _totalBonusPoints -= bonusPoints;
                }

                _selectedMemberships.Remove(selectedMembership);

                UpdateTotalCost();
                UpdateBonusPoints();
            }
        }

        private decimal CalculateBonusPoints(Seasontickets membership, int visits)
        {
            // Базовые бонусные баллы - 5% от стоимости
            decimal basePoints = membership.Price.GetValueOrDefault() * 0.05m;

            // Дополнительные баллы за количество занятий
            decimal visitMultiplier;
            if (visits == 8)
                visitMultiplier = 1.2m;   // +20% для 8 занятий
            else if (visits == 12)
                visitMultiplier = 1.5m;   // +50% для 12 занятий
            else
                visitMultiplier = 1.0m;   // Без бонуса для других значений

            return Math.Round(basePoints * visitMultiplier, 2);
        }

        private void UpdateBonusPoints()
        {
            if (BonusPointsTextBlock == null)
                return;

            BonusPointsTextBlock.Text = $"{_totalBonusPoints:F2}";
        }

        private void RemainingVisitsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTotalCost();
        }

        private void DiscountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotalCost();
        }

        private void UpdateTotalCost()
        {
            if (TotalCostTextBlock == null || PriceSoldTextBox == null ||
                DiscountTextBox == null || _selectedMemberships == null)
                return;

            decimal totalCost = 0;

            // Суммируем стоимость всех выбранных абонементов
            foreach (var membership in _selectedMemberships)
            {
                totalCost += membership.Price * membership.Visits;
            }

            if (decimal.TryParse(DiscountTextBox.Text, out decimal discount))
            {
                totalCost -= discount;
            }

            if (totalCost < 0) totalCost = 0;

            TotalCostTextBlock.Text = $"Итого к оплате: {totalCost:C}";
            PriceSoldTextBox.Text = totalCost.ToString();
        }

        private void SaveSaleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null)
            {
                MessageBox.Show("Пожалуйста, выберите клиента.");
                return;
            }

            if (AdministratorComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите администратора.");
                return;
            }

            if (PaymentMethodComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите способ оплаты.");
                return;
            }

            if (MembershipRadioButton.IsChecked == true && _selectedMemberships.Count == 0)
            {
                MessageBox.Show("Пожалуйста, добавьте хотя бы один абонемент.");
                return;
            }

            if (ServiceRadioButton.IsChecked == true && ServiceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите услугу.");
                return;
            }

            int clientId = _selectedClient.ClientID;
            DateTime saleDateTime = SaleDatePicker.SelectedDate ?? DateTime.Now;
            decimal discountAmount = decimal.TryParse(DiscountTextBox.Text, out discountAmount) ? discountAmount : 0;
            decimal priceSold = decimal.TryParse(PriceSoldTextBox.Text, out priceSold) ? priceSold : 0;
            string statusSale = StatusSaleComboBox.SelectedItem as string ?? "Активна";
            int vatRateId = VatRateComboBox.SelectedValue != null ? (int)VatRateComboBox.SelectedValue : 1;
            int administratorId = (int)AdministratorComboBox.SelectedValue;
            int? trainerId = TrainerComboBox.SelectedValue != null ? (int?)TrainerComboBox.SelectedValue : null;
            DateTime? startDateTime = StartDateTimePicker.SelectedDate;
            DateTime? endDateTime = EndDateTimePicker.SelectedDate;
            int paymentMethodId = (int)PaymentMethodComboBox.SelectedValue;
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (MembershipRadioButton.IsChecked == true)
                    {
                        // Создаем основную продажу
                        var newSale = new Sales
                        {
                            AdministratorID = administratorId,
                            SaleDateTime = saleDateTime,
                            DiscountAmount = discountAmount,
                            PriceSold = priceSold,
                            StatusSale = statusSale,
                            VatRateID = vatRateId,
                            ClassificationID = 1, // Классификация для абонементов
                            StartDateTime = startDateTime,
                            EndDateTime = endDateTime,
                            TrainerID = trainerId
                        };

                        _context.Sales.Add(newSale);
                        _context.SaveChanges();

                        // Добавляем все выбранные абонементы
                        foreach (var selectedMembership in _selectedMemberships)
                        {
                            // Создаем запись в промежуточной таблице SeasonticketClients
                            var seasonticketClient = new SeasonticketClients
                            {
                                SeasonticketID = selectedMembership.SeasonticketID,
                                ClientID = clientId
                            };
                            _context.SeasonticketClients.Add(seasonticketClient);
                            _context.SaveChanges();

                            // Создаем запись в таблице SeasonticketSales
                            var seasonticketSale = new SeasonticketSales
                            {
                                SaleID = newSale.SaleID,
                                SeasonticketID = selectedMembership.SeasonticketID
                            };
                            _context.SeasonticketSales.Add(seasonticketSale);

                            // Обновляем информацию о продаже
                            newSale.SeasonticketID = selectedMembership.SeasonticketID;
                            newSale.RemainingVisits = selectedMembership.Visits;
                            _context.SaveChanges();
                        }

                        // Создаем платеж
                        var payment = new Payments
                        {
                            SaleID = newSale.SaleID,
                            Amount = priceSold,
                            PaymentMethodID = paymentMethodId,
                            DateTime = DateTime.Now
                        };

                        _context.Payments.Add(payment);

                        // Начисляем бонусные баллы клиенту
                        var client = _context.Clients.Find(clientId);
                        if (client != null)
                        {
                            client.BonuseBalance = (client.BonuseBalance ?? 0) + _totalBonusPoints;
                        }

                        _context.SaveChanges();
                        transaction.Commit();

                        MessageBox.Show($"Продажа успешно оформлена! Начислено {_totalBonusPoints:F2} бонусных баллов.");
                        DialogResult = true;
                        Close();
                    }
                    else if (ServiceRadioButton.IsChecked == true && ServiceComboBox.SelectedItem != null)
                    {
                        int serviceId = (int)ServiceComboBox.SelectedValue;

                        // Создаем связь услуги
                        var seasonticketService = new SeasonticketServices
                        {
                            ServiceID = serviceId,
                            AccessAllowed = true,
                            VisitLimit = 1,
                            DateTime = DateTime.Now
                        };

                        _context.SeasonticketServices.Add(seasonticketService);
                        _context.SaveChanges();

                        // Создаем продажу
                        var newSale = new Sales
                        {
                            AdministratorID = administratorId,
                            TrainerID = trainerId,
                            SeasonticketServiceID = seasonticketService.SeasonticketServiceID,
                            SaleDateTime = saleDateTime,
                            DiscountAmount = discountAmount,
                            PriceSold = priceSold,
                            RemainingVisits = 1,
                            StatusSale = statusSale,
                            VatRateID = vatRateId,
                            ClassificationID = 2 // Классификация для услуг
                        };

                        _context.Sales.Add(newSale);
                        _context.SaveChanges();

                        // Обновляем связь с продажей
                        seasonticketService.SaleID = newSale.SaleID;
                        _context.SaveChanges();

                        // Создаем платеж
                        var payment = new Payments
                        {
                            SaleID = newSale.SaleID,
                            Amount = priceSold,
                            PaymentMethodID = paymentMethodId,
                            DateTime = DateTime.Now
                        };

                        _context.Payments.Add(payment);
                        _context.SaveChanges();

                        transaction.Commit();

                        MessageBox.Show("Продажа услуги успешно оформлена!");
                        DialogResult = true;
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ClientInfo
    {
        public int ClientID { get; set; }
        public string FullName { get; set; }
        public string CardNumber { get; set; }
        public decimal BonusPoints { get; set; }

        public override string ToString()
        {
            return $"{FullName} (Карта №{CardNumber})";
        }
    }

    public class ServiceTrainerCollection
    {
        public int TrainerID { get; set; }
        public string Name { get; set; }
    }

    public class StaffsCollection
    {
        public int StaffID { get; set; }
        public string Name { get; set; }
    }

    public class SelectedMembership
    {
        public int SeasonticketID { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Visits { get; set; }
    }
}

