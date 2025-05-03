using FitnessCenterIS.Model;
using FitnessCenterIS.View.Pages;
using FitnessCenterIS.View.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;

namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for VisitClientWindow.xaml
    /// </summary>
    public partial class VisitClientWindow : Window
    {
        private ObservableCollection<Clients> _clientList;
        private MenuWindow _menuWindow;
        private int _currentUserRole; // Роль текущего пользователя
        private int _currentUserId; // ID текущего пользователя

        public VisitClientWindow(ObservableCollection<Clients> clientList, MenuWindow menuWindow, int currentUserId = 0)
        {
            InitializeComponent();
            _clientList = clientList;
            _menuWindow = menuWindow;
            _currentUserId = currentUserId;

            // Получаем роль текущего пользователя
            _currentUserRole = GetCurrentUserRole(currentUserId);
        }

        // Метод для получения роли текущего пользователя
        private int GetCurrentUserRole(int userId)
        {
            if (userId <= 0)
                return 0; // Если ID пользователя не задан

            using (var context = new BDFitnessClubDipEntities())
            {
                var user = context.Users
                    .Include(u => u.Staffs)
                    .Include(u => u.Staffs.Roles)
                    .FirstOrDefault(u => u.UserID == userId);

                if (user?.Staffs?.Roles != null)
                {
                    return user.Staffs.Roles.RoleID;
                }
            }

            return 0; // Если роль не найдена или пользователь не авторизован
        }

        private void FindClientByCardNumber_Click(object sender, RoutedEventArgs e)
        {
            string cardNumber = CardNumberTextBox.Text;
            if (!string.IsNullOrEmpty(cardNumber))
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    // Сначала проверяем, является ли карта принадлежащей администратору
                    var restrictedStaff = context.Staffs
                        .Include(s => s.Persons)
                        .Include(s => s.Roles)
                        .FirstOrDefault(s => s.Persons.NumberCard == cardNumber &&
                                      (s.Roles.Name == "Администратор стойки" || s.Roles.Name == "Системный администратор"));

                    // Если карта принадлежит администратору и текущий пользователь - Администратор стойки
                    if (restrictedStaff != null && IsCurrentUserAdminDesk())
                    {
                        MessageBox.Show("У вас недостаточно прав для просмотра профиля этого сотрудника.",
                            "Ограничение доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Если прошли проверку, выполняем стандартный поиск
                    var client = context.Clients
                        .Include(c => c.Persons)
                        .FirstOrDefault(c => c.Persons.NumberCard == cardNumber);

                    var staff = context.Staffs
                        .Include(s => s.Persons)
                        .Include(s => s.Roles)
                        .FirstOrDefault(s => s.Persons.NumberCard == cardNumber);

                    if (client != null)
                    {
                        var profileClientPage = new ProfileClient(client.ClientID);
                        _menuWindow.MainFrame.Navigate(profileClientPage);
                        this.Close();
                    }
                    else if (staff != null)
                    {
                        var profileStaffPage = new ProfileStaff(staff.StaffID);
                        _menuWindow.MainFrame.Navigate(profileStaffPage);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Профиль с номером карты {cardNumber} не найден.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, введите номер карты.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Проверка, является ли текущий пользователь Администратором стойки
        private bool IsCurrentUserAdminDesk()
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                // Получаем ID роли Администратора стойки
                var adminDeskRole = context.Roles.FirstOrDefault(r => r.Name == "Администратор стойки");
                if (adminDeskRole == null)
                    return false;

                return _currentUserRole == adminDeskRole.RoleID;
            }
        }

        // Метод проверки ограничения доступа
        private bool IsAdminRoleRestricted(int currentUserRoleId, int targetUserRoleId)
        {
            // Получаем ID ролей из базы данных, если они еще не известны
            int adminDeskRoleId = GetRoleId("Администратор стойки");
            int sysAdminRoleId = GetRoleId("Системный администратор");

            // Если текущий пользователь - Администратор стойки, а целевой сотрудник - Админ стойки или Системный админ
            return currentUserRoleId == adminDeskRoleId &&
                   (targetUserRoleId == adminDeskRoleId || targetUserRoleId == sysAdminRoleId);
        }

        // Получение ID роли по названию
        private int GetRoleId(string roleName)
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                var role = context.Roles.FirstOrDefault(r => r.Name == roleName);
                return role?.RoleID ?? 0;
            }
        }

        private void StartQRCodeScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_clientList != null && _clientList.Any())
            {
                var clientsCollectionList = _clientList.Select(c => new ClientsCollection
                {
                    ClientID = c.ClientID,
                    FullName = c.Persons?.Surname + " " + c.Persons?.Name + " " + c.Persons?.MiddleName,
                    // Add other properties as needed for ClientsCollection
                }).ToList();

                QRCodeWindow scanWindow = new QRCodeWindow(clientsCollectionList, _menuWindow, _currentUserRole, _currentUserId); // Передаем роль и ID пользователя
                scanWindow.QRCodeScanned += ScanWindow_QRCodeScanned;

                this.Close();
                scanWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Список клиентов не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScanWindow_QRCodeScanned(string cardNumber)
        {
            if (!string.IsNullOrEmpty(cardNumber))
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var client = context.Clients
                        .Include(c => c.Persons)
                        .FirstOrDefault(c => c.Persons.NumberCard == cardNumber);

                    var staff = context.Staffs
                        .Include(s => s.Persons)
                        .Include(s => s.Roles)
                        .FirstOrDefault(s => s.Persons.NumberCard == cardNumber);

                    if (client != null)
                    {
                        var profileClientPage = new ProfileClient(client.ClientID);
                        _menuWindow.MainFrame.Navigate(profileClientPage);
                        this.Close();
                    }
                    else if (staff != null)
                    {
                        // Проверяем доступ Администратора стойки к профилям других администраторов
                        if (IsAdminRoleRestricted(_currentUserRole, staff.Roles.RoleID))
                        {
                            MessageBox.Show("У вас нет доступа к профилю этого сотрудника.", "Ограничение доступа",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var profileStaffPage = new ProfileStaff(staff.StaffID);
                        _menuWindow.MainFrame.Navigate(profileStaffPage);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Профиль с номером карты {cardNumber} не найден.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Отсканированный QR-код не содержит номера карты.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}