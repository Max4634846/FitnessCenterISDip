using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using FitnessCenterIS.Model;

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
                RemoveMembershipButton == null || TrainingTypeComboBox == null ||
                GroupSelectionPanel == null)
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

            // По умолчанию выбираем индивидуальный тип тренировки
            TrainingTypeComboBox.SelectedIndex = 0; // Индивидуальное

            // Начально скрываем панель выбора группы
            GroupSelectionPanel.Visibility = Visibility.Collapsed;

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

            // Фильтруем абонементы и услуги для индивидуального типа тренировки
            FilterMembershipsAndServices("Индивидуальное");
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

            // Загрузка групп
            var groups = _context.Groups
                .Where(g => g.StatusActivity == "Активно")
                .ToList();

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

            GroupComboBox.ItemsSource = groups;
            GroupComboBox.DisplayMemberPath = "Name";
            GroupComboBox.SelectedValuePath = "GroupID";
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupComboBox.SelectedItem is Groups selectedGroup &&
                ServiceRadioButton.IsChecked == true)
            {
                // Для услуг фильтруем сервисы по выбранной группе
                var groupServices = _context.Services
                    .Where(s => s.ServiceID == selectedGroup.ServiceID)
                    .ToList();

                ServiceComboBox.ItemsSource = groupServices;
            }
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

                // Если выбрана группа, обновляем список услуг
                if (GroupComboBox.SelectedItem is Groups selectedGroup)
                {
                    var groupServices = _context.Services
                        .Where(s => s.ServiceID == selectedGroup.ServiceID)
                        .ToList();

                    ServiceComboBox.ItemsSource = groupServices;
                }
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
            if (ServiceComboBox == null || PriceSoldTextBox == null || TrainerComboBox == null)
                return;

            if (ServiceComboBox.SelectedItem is Services selectedService)
            {
                _basePrice = selectedService.Price.GetValueOrDefault();
                PriceSoldTextBox.Text = _basePrice.ToString();

                // Автоматически выбираем тренера, связанного с услугой
                var serviceTrainers = _context.ServiceTrainer
                    .Where(st => st.ServiceID == selectedService.ServiceID)
                    .Select(st => st.TrainerID)
                    .ToList();

                if (serviceTrainers.Any())
                {
                    // Находим тренера в списке доступных тренеров
                    var trainer = TrainerComboBox.Items.Cast<ServiceTrainerCollection>()
                        .FirstOrDefault(t => serviceTrainers.Contains(t.TrainerID));

                    if (trainer != null)
                    {
                        TrainerComboBox.SelectedItem = trainer;
                        // Делаем выбор тренера недоступным для изменения
                        TrainerComboBox.IsEnabled = false;
                    }
                }
                else
                {
                    // Если тренер не назначен для услуги, разрешаем ручной выбор
                    TrainerComboBox.IsEnabled = true;
                }
            }
            else
            {
                PriceSoldTextBox.Text = "";
                TrainerComboBox.IsEnabled = true;
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

            // Проверка на выбор группы только для групповых занятий
            bool isGroupTraining = false;

            if (TrainingTypeComboBox.SelectedItem is ComboBoxItem trainingTypeItem)
            {
                isGroupTraining = trainingTypeItem.Content.ToString() == "Групповое";
            }

            // Проверяем выбор группы только если это групповое занятие
            if (isGroupTraining && GroupComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите группу для группового занятия.");
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
            int? groupId = isGroupTraining ? (int?)GroupComboBox.SelectedValue : null;
            DateTime saleDateTime = SaleDatePicker.SelectedDate ?? DateTime.Now;
            decimal discountAmount = decimal.TryParse(DiscountTextBox.Text, out discountAmount) ? discountAmount : 0;
            decimal priceSold = decimal.TryParse(PriceSoldTextBox.Text, out priceSold) ? priceSold : 0;
            string statusSale = StatusSaleComboBox.SelectedItem as string ?? "Активна";
            int vatRateId = VatRateComboBox.SelectedValue != null ? (int)VatRateComboBox.SelectedValue : 1;
            int administratorId = (int)AdministratorComboBox.SelectedValue;
            int? trainerId = TrainerComboBox.SelectedValue != null ? (int?)TrainerComboBox.SelectedValue : null;
            DateTime? startDateTime = StartDateTimePicker.SelectedDate;
            DateTime? endDateTime = EndDateTimePicker.SelectedDate;

            // Сохраняем ID созданной продажи, чтобы использовать в обработке оплаты
            int createdSaleId = 0;

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
                        createdSaleId = newSale.SaleID;

                        // Добавляем все выбранные абонементы
                        foreach (var membership in _selectedMemberships)
                        {
                            // Создаем запись в промежуточной таблице SeasonticketClients
                            var seasonticketClient = new SeasonticketClients
                            {
                                SeasonticketID = membership.SeasonticketID,
                                ClientID = clientId
                            };
                            _context.SeasonticketClients.Add(seasonticketClient);
                            _context.SaveChanges();

                            // Создаем запись в таблице SeasonticketSales
                            var seasonticketSale = new SeasonticketSales
                            {
                                SaleID = newSale.SaleID,
                                SeasonticketID = membership.SeasonticketID
                            };
                            _context.SeasonticketSales.Add(seasonticketSale);

                            // Обновляем информацию о продаже
                            newSale.SeasonticketID = membership.SeasonticketID;
                            newSale.RemainingVisits = membership.Visits;
                            _context.SaveChanges();

                            // Если это групповое занятие, создаем связь с группой
                            if (isGroupTraining && groupId.HasValue)
                            {
                                // Находим услугу, связанную с группой
                                int? serviceId = null;
                                var group = _context.Groups.Find(groupId.Value);
                                if (group != null)
                                {
                                    serviceId = group.ServiceID;
                                }

                                // Создаем связь услуги с абонементом
                                var seasonticketService = new SeasonticketServices
                                {
                                    SeasonticketID = membership.SeasonticketID,
                                    ServiceID = serviceId,
                                    AccessAllowed = true,
                                    VisitLimit = membership.Visits,
                                    DateTime = DateTime.Now,
                                    SaleID = newSale.SaleID
                                };

                                _context.SeasonticketServices.Add(seasonticketService);
                                _context.SaveChanges();

                                // Добавляем клиента в группу
                                var groupMember = new GroupMembers
                                {
                                    GroupID = groupId.Value,
                                    SeasonticketServiceID = seasonticketService.SeasonticketServiceID,
                                    CreateDateTime = DateTime.Now,
                                    Notes = $"Добавлен при продаже абонемента {membership.Name}"
                                };

                                _context.GroupMembers.Add(groupMember);
                                _context.SaveChanges();
                            }
                        }

                        // Открываем окно для выбора способа оплаты
                        var depositWindow = new DepositAccountWindow(clientId, priceSold, "Payment");
                        if (depositWindow.ShowDialog() == true && depositWindow.PaymentResult.Success)
                        {
                            // Используем данные из окна оплаты
                            var paymentResult = depositWindow.PaymentResult;

                            // Если есть сумма к оплате картой, создаем запись платежа
                            if (paymentResult.CardAmount > 0)
                            {
                                var payment = new Payments
                                {
                                    SaleID = createdSaleId,
                                    Amount = paymentResult.CardAmount,
                                    PaymentMethodID = paymentResult.PaymentMethodId,
                                    DateTime = DateTime.Now
                                };

                                _context.Payments.Add(payment);
                                _context.SaveChanges();
                            }

                            // Начисляем бонусные баллы только за часть, оплаченную не бонусами
                            if (paymentResult.BonusAmount < priceSold)
                            {
                                decimal bonusPointsBase = priceSold - paymentResult.BonusAmount;
                                decimal bonusToAdd = CalculateBonusPointsForSale(bonusPointsBase);

                                // Начисляем бонусные баллы клиенту
                                var client = _context.Clients.Find(clientId);
                                if (client != null)
                                {
                                    client.BonuseBalance = (client.BonuseBalance ?? 0) + bonusToAdd;
                                    _context.SaveChanges();
                                }
                            }

                            transaction.Commit();

                            string paymentDetails = "";
                            if (paymentResult.CardAmount > 0)
                                paymentDetails += $"{paymentResult.CardAmount:N2} ₽ картой";
                            if (paymentResult.DepositAmount > 0)
                            {
                                if (!string.IsNullOrEmpty(paymentDetails)) paymentDetails += ", ";
                                paymentDetails += $"{paymentResult.DepositAmount:N2} ₽ с депозита";
                            }
                            if (paymentResult.BonusAmount > 0)
                            {
                                if (!string.IsNullOrEmpty(paymentDetails)) paymentDetails += ", ";
                                paymentDetails += $"{paymentResult.BonusAmount:N2} бонусными баллами";
                            }

                            // Рассчитываем и отображаем начисленные бонусы
                            decimal bonusCalculated = 0;
                            if (paymentResult.BonusAmount < priceSold)
                            {
                                decimal bonusPointsBase = priceSold - paymentResult.BonusAmount;
                                bonusCalculated = CalculateBonusPointsForSale(bonusPointsBase);
                            }

                            string successMessage = $"Продажа успешно оформлена!\nОплата: {paymentDetails}";
                            if (bonusCalculated > 0)
                            {
                                successMessage += $"\nНачислено {bonusCalculated:N2} бонусных баллов";
                            }

                            if (isGroupTraining)
                                successMessage += "\nКлиент добавлен в группу.";

                            MessageBox.Show(successMessage);

                            ReceiptPrinter.ShowReceiptPreview(newSale, paymentResult);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            // Пользователь отменил оплату, отменяем транзакцию
                            transaction.Rollback();
                            MessageBox.Show("Продажа отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
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
                        createdSaleId = newSale.SaleID;

                        // Обновляем связь с продажей
                        seasonticketService.SaleID = newSale.SaleID;
                        _context.SaveChanges();

                        // Если это групповая услуга, добавляем клиента в группу
                        if (isGroupTraining && groupId.HasValue)
                        {
                            // Добавляем клиента в группу
                            var groupMember = new GroupMembers
                            {
                                GroupID = groupId.Value,
                                SeasonticketServiceID = seasonticketService.SeasonticketServiceID,
                                CreateDateTime = DateTime.Now,
                                Notes = "Добавлен при продаже услуги"
                            };

                            _context.GroupMembers.Add(groupMember);
                            _context.SaveChanges();
                        }

                        // Открываем окно для выбора способа оплаты
                        var depositWindow = new DepositAccountWindow(clientId, priceSold, "Payment");
                        if (depositWindow.ShowDialog() == true && depositWindow.PaymentResult.Success)
                        {
                            // Используем данные из окна оплаты
                            var paymentResult = depositWindow.PaymentResult;

                            // Если есть сумма к оплате картой, создаем запись платежа
                            if (paymentResult.CardAmount > 0)
                            {
                                var payment = new Payments
                                {
                                    SaleID = createdSaleId,
                                    Amount = paymentResult.CardAmount,
                                    PaymentMethodID = paymentResult.PaymentMethodId,
                                    DateTime = DateTime.Now
                                };

                                _context.Payments.Add(payment);
                                _context.SaveChanges();
                            }

                            // Начисляем бонусные баллы только за часть, оплаченную не бонусами
                            if (paymentResult.BonusAmount < priceSold)
                            {
                                decimal bonusPointsBase = priceSold - paymentResult.BonusAmount;
                                decimal bonusToAdd = CalculateBonusPointsForSale(bonusPointsBase);

                                // Начисляем бонусные баллы клиенту
                                var client = _context.Clients.Find(clientId);
                                if (client != null)
                                {
                                    client.BonuseBalance = (client.BonuseBalance ?? 0) + bonusToAdd;
                                    _context.SaveChanges();
                                }
                            }

                            transaction.Commit();

                            string paymentDetails = "";
                            if (paymentResult.CardAmount > 0)
                                paymentDetails += $"{paymentResult.CardAmount:N2} ₽ картой";
                            if (paymentResult.DepositAmount > 0)
                            {
                                if (!string.IsNullOrEmpty(paymentDetails)) paymentDetails += ", ";
                                paymentDetails += $"{paymentResult.DepositAmount:N2} ₽ с депозита";
                            }
                            if (paymentResult.BonusAmount > 0)
                            {
                                if (!string.IsNullOrEmpty(paymentDetails)) paymentDetails += ", ";
                                paymentDetails += $"{paymentResult.BonusAmount:N2} бонусными баллами";
                            }

                            // Рассчитываем и отображаем начисленные бонусы
                            decimal bonusCalculated = 0;
                            if (paymentResult.BonusAmount < priceSold)
                            {
                                decimal bonusPointsBase = priceSold - paymentResult.BonusAmount;
                                bonusCalculated = CalculateBonusPointsForSale(bonusPointsBase);
                            }

                            string successMessage = $"Продажа услуги успешно оформлена!\nОплата: {paymentDetails}";
                            if (bonusCalculated > 0)
                            {
                                successMessage += $"\nНачислено {bonusCalculated:N2} бонусных баллов";
                            }

                            if (isGroupTraining)
                                successMessage += "\nКлиент добавлен в группу.";

                            MessageBox.Show(successMessage);

                            ReceiptPrinter.ShowReceiptPreview(newSale, paymentResult);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            // Пользователь отменил оплату, отменяем транзакцию
                            transaction.Rollback();
                            MessageBox.Show("Продажа отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Новый метод для расчета бонусных баллов за покупку
        private decimal CalculateBonusPointsForSale(decimal amountPaid)
        {
            // Базовые бонусные баллы - 5% от стоимости
            decimal bonusPoints = amountPaid * 0.05m;

            // Дополнительные баллы за количество занятий (если это абонемент)
            if (MembershipRadioButton.IsChecked == true && RemainingVisitsComboBox.SelectedItem is ComboBoxItem selectedVisitsItem)
            {
                int visits = int.Parse(selectedVisitsItem.Content.ToString());
                decimal visitMultiplier;

                if (visits == 8)
                    visitMultiplier = 1.2m;   // +20% для 8 занятий
                else if (visits == 12)
                    visitMultiplier = 1.5m;   // +50% для 12 занятий
                else
                    visitMultiplier = 1.0m;   // Без бонуса для других значений

                bonusPoints *= visitMultiplier;
            }

            return Math.Round(bonusPoints, 2);
        }

        // Метод обработки изменения типа тренировки
        private void TrainingTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrainingTypeComboBox == null || GroupSelectionPanel == null)
                return;

            // Получаем выбранный тип тренировки
            if (TrainingTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string trainingType = selectedItem.Content.ToString();

                // Показываем или скрываем панель выбора группы в зависимости от типа тренировки
                if (trainingType == "Групповое")
                {
                    GroupSelectionPanel.Visibility = Visibility.Visible;
                    LoadGroups(); // Загружаем список групп
                }
                else
                {
                    GroupSelectionPanel.Visibility = Visibility.Collapsed;
                }

                // Фильтруем абонементы/услуги в зависимости от типа тренировки
                FilterMembershipsAndServices(trainingType);
            }
        }

        // Метод для загрузки групп в ComboBox
        private void LoadGroups()
        {
            try
            {
                // Загружаем только активные группы
                var groups = _context.Groups
                    .Where(g => g.StatusActivity == "Активно")
                    .OrderBy(g => g.Name)
                    .ToList();

                GroupComboBox.ItemsSource = groups;

                // Если есть группы, выбираем первую
                if (groups.Count > 0)
                    GroupComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке групп: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для фильтрации абонементов и услуг по типу тренировки
        // Метод для фильтрации абонементов и услуг по типу тренировки
        private void FilterMembershipsAndServices(string trainingType)
        {
            try
            {
                if (MembershipComboBox == null || ServiceComboBox == null)
                    return;

                // Загружаем все абонементы (без фильтрации)
                var memberships = _context.Seasontickets.ToList();
                MembershipComboBox.ItemsSource = memberships;

                // Загружаем все активные услуги
                var services = _context.Services
                    .Where(s => s.StatusService == "Активен")
                    .ToList();

                ServiceComboBox.ItemsSource = services;

                // Если комбобоксы пустые, покажем сообщение в консоль для диагностики
                if (memberships.Count == 0)
                {
                    Console.WriteLine("Внимание: список абонементов пуст");
                }

                if (services.Count == 0)
                {
                    Console.WriteLine("Внимание: список услуг пуст");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке абонементов и услуг: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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