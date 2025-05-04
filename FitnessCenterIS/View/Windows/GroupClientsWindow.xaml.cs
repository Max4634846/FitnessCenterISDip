using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using FitnessCenterIS.Model;
using System.Windows.Data;

namespace FitnessCenterIS.View.Windows
{
    public partial class GroupClientsWindow : Window
    {
        private BDFitnessClubDipEntities _context;
        private ObservableCollection<GroupViewModel> _groups;
        private ObservableCollection<GroupClientViewModel> _groupClients;
        private CollectionViewSource _groupsViewSource;
        private CollectionViewSource _clientsViewSource;

        public GroupClientsWindow()
        {
            InitializeComponent();
            _context = new BDFitnessClubDipEntities();
            _groups = new ObservableCollection<GroupViewModel>();
            _groupClients = new ObservableCollection<GroupClientViewModel>();

            // Настраиваем источники представления для фильтрации
            _groupsViewSource = new CollectionViewSource();
            _groupsViewSource.Source = _groups;
            _clientsViewSource = new CollectionViewSource();
            _clientsViewSource.Source = _groupClients;

            this.Loaded += (s, e) => LoadGroups();
        }

        private void LoadGroups()
        {
            try
            {
                _groups.Clear();

                var groupsData = _context.Groups
                    .Include(g => g.Services)
                    .Where(g => g.StatusActivity == "Активно")
                    .ToList();

                foreach (var group in groupsData)
                {
                    // Подсчет количества клиентов в группе
                    int membersCount = _context.GroupMembers
                        .Count(gm => gm.GroupID == group.GroupID);

                    _groups.Add(new GroupViewModel
                    {
                        GroupID = group.GroupID,
                        Name = group.Name,
                        ServiceName = group.Services?.Name,
                        MembersCount = membersCount
                    });
                }

                GroupsListBox.ItemsSource = _groupsViewSource.View;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке групп: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGroupClients(int groupId)
        {
            try
            {
                _groupClients.Clear();

                var selectedGroup = _context.Groups
                    .FirstOrDefault(g => g.GroupID == groupId);

                if (selectedGroup == null)
                    return;

                ClientsHeaderTextBlock.Text = $"Клиенты в группе: {selectedGroup.Name}";

                // Получаем всех клиентов группы через GroupMembers, но с более простым запросом
                var groupMembers = _context.GroupMembers
                    .Where(gm => gm.GroupID == groupId)
                    .ToList();

                foreach (var member in groupMembers)
                {
                    if (member.SeasonticketServiceID == null)
                        continue;

                    // Получаем SeasonticketService для этого члена группы
                    var seasonticketService = _context.SeasonticketServices
                        .Find(member.SeasonticketServiceID);

                    if (seasonticketService == null)
                        continue;

                    // Получаем информацию о продаже
                    var sale = _context.Sales
                        .FirstOrDefault(s => s.SaleID == seasonticketService.SaleID);

                    if (sale == null)
                        continue;

                    // Находим абонемент (если есть)
                    string membershipName = "Неизвестно";
                    if (seasonticketService.SeasonticketID.HasValue)
                    {
                        var seasonticket = _context.Seasontickets
                            .Find(seasonticketService.SeasonticketID.Value);
                        if (seasonticket != null)
                            membershipName = seasonticket.Name;
                    }
                    // Или услугу
                    else if (seasonticketService.ServiceID.HasValue)
                    {
                        var service = _context.Services
                            .Find(seasonticketService.ServiceID.Value);
                        if (service != null)
                            membershipName = service.Name;
                    }

                    // Ищем клиента через SeasonticketClients
                    int? clientId = null;
                    if (sale.SeasonticketID.HasValue)
                    {
                        var seasonticketClient = _context.SeasonticketClients
                            .FirstOrDefault(sc => sc.SeasonticketID == sale.SeasonticketID);
                        if (seasonticketClient != null)
                            clientId = seasonticketClient.ClientID;
                    }

                    // Если не нашли через абонемент, то ищем альтернативными способами
                    if (clientId == null)
                    {
                        // Можно добавить логику для поиска клиентов, связанных с продажей иными способами
                        // Например, через другие связанные таблицы
                        continue;
                    }

                    // Получаем данные о клиенте
                    var client = _context.Clients
                        .Include(c => c.Persons)
                        .FirstOrDefault(c => c.ClientID == clientId);

                    if (client == null)
                        continue;

                    _groupClients.Add(new GroupClientViewModel
                    {
                        ClientID = client.ClientID,
                        GroupMemberID = member.GroupMemberID,
                        FullName = $"{client.Persons.Surname} {client.Persons.Name} {client.Persons.MiddleName}",
                        Membership = membershipName,
                        StartDate = sale.StartDateTime ?? member.CreateDateTime,
                        RemainingVisits = sale.RemainingVisits ?? 0,
                        Status = sale.StatusSale ?? "Неизвестно"
                    });
                }

                ClientsDataGrid.ItemsSource = _clientsViewSource.View;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке клиентов группы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsListBox.SelectedItem is GroupViewModel selectedGroup)
            {
                LoadGroupClients(selectedGroup.GroupID);
            }
            else
            {
                _groupClients.Clear();
                ClientsHeaderTextBlock.Text = "Клиенты в группе: Не выбрана";
            }
        }

        private void GroupSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = GroupSearchTextBox.Text.ToLower();

            _groupsViewSource.View.Filter = item =>
            {
                if (item is GroupViewModel group)
                {
                    return string.IsNullOrWhiteSpace(searchText) ||
                           group.Name.ToLower().Contains(searchText) ||
                           group.ServiceName?.ToLower().Contains(searchText) == true;
                }
                return false;
            };
        }

        private void ClientSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = ClientSearchTextBox.Text.ToLower();

            _clientsViewSource.View.Filter = item =>
            {
                if (item is GroupClientViewModel client)
                {
                    return string.IsNullOrWhiteSpace(searchText) ||
                           client.FullName.ToLower().Contains(searchText) ||
                           client.Membership.ToLower().Contains(searchText) ||
                           client.Status.ToLower().Contains(searchText);
                }
                return false;
            };
        }

        private void RefreshGroups_Click(object sender, RoutedEventArgs e)
        {
            LoadGroups();
        }

        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsListBox.SelectedItem is GroupViewModel selectedGroup)
            {
                // Для добавления клиента через продажу абонемента/услуги
                // открываем окно продажи
                var clientSelectWindow = new ClientSelectionWindow();
                if (clientSelectWindow.ShowDialog() == true)
                {
                    int clientId = clientSelectWindow.SelectedClientId;
                    var newSaleWindow = new NewSaleWindow(clientId);
                    if (newSaleWindow.ShowDialog() == true)
                    {
                        // После успешной продажи обновляем список клиентов в группе
                        LoadGroupClients(selectedGroup.GroupID);
                    }
                }
            }
            else
            {
                MessageBox.Show("Сначала выберите группу, в которую нужно добавить клиента.",
                    "Группа не выбрана", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is GroupClientViewModel selectedClient)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить клиента {selectedClient.FullName} из группы?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var groupMember = _context.GroupMembers
                            .Find(selectedClient.GroupMemberID);

                        if (groupMember != null)
                        {
                            _context.GroupMembers.Remove(groupMember);
                            _context.SaveChanges();

                            // Обновляем список после удаления
                            if (GroupsListBox.SelectedItem is GroupViewModel selectedGroup)
                            {
                                LoadGroupClients(selectedGroup.GroupID);
                                // Обновляем также количество клиентов в группе
                                LoadGroups();
                            }

                            MessageBox.Show("Клиент успешно удален из группы.",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении клиента из группы: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите клиента для удаления из группы.",
                    "Клиент не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FreezeClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is GroupClientViewModel selectedClient)
            {
                // Получаем продажу, связанную с клиентом
                var groupMember = _context.GroupMembers
                    .Find(selectedClient.GroupMemberID);

                if (groupMember?.SeasonticketServices?.SaleID != null)
                {
                    int saleId = groupMember.SeasonticketServices.SaleID.Value;
                    var sale = _context.Sales.Find(saleId);

                    if (sale != null)
                    {
                        sale.StatusSale = "Заморожена";
                        _context.SaveChanges();

                        // Обновляем список клиентов
                        if (GroupsListBox.SelectedItem is GroupViewModel selectedGroup)
                        {
                            LoadGroupClients(selectedGroup.GroupID);
                        }

                        MessageBox.Show("Участие клиента в группе заморожено.",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите клиента для заморозки участия.",
                    "Клиент не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UnfreezeClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is GroupClientViewModel selectedClient)
            {
                // Получаем продажу, связанную с клиентом
                var groupMember = _context.GroupMembers
                    .Find(selectedClient.GroupMemberID);

                if (groupMember?.SeasonticketServices?.SaleID != null)
                {
                    int saleId = groupMember.SeasonticketServices.SaleID.Value;
                    var sale = _context.Sales.Find(saleId);

                    if (sale != null)
                    {
                        sale.StatusSale = "Активна";
                        _context.SaveChanges();

                        // Обновляем список клиентов
                        if (GroupsListBox.SelectedItem is GroupViewModel selectedGroup)
                        {
                            LoadGroupClients(selectedGroup.GroupID);
                        }

                        MessageBox.Show("Участие клиента в группе возобновлено.",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите клиента для возобновления участия.",
                    "Клиент не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class GroupViewModel
    {
        public int GroupID { get; set; }
        public string Name { get; set; }
        public string ServiceName { get; set; }
        public int MembersCount { get; set; }
    }

    public class GroupClientViewModel
    {
        public int ClientID { get; set; }
        public int GroupMemberID { get; set; }
        public string FullName { get; set; }
        public string Membership { get; set; }
        public DateTime? StartDate { get; set; }
        public int RemainingVisits { get; set; }
        public string Status { get; set; }
    }
}