using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddEditUserWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private bool _isEditMode = false;
        private int _userId;
        private List<StaffInfo> _allStaffs = new List<StaffInfo>();

        // Класс для отображения информации о сотруднике
        public class StaffInfo
        {
            public int StaffID { get; set; }
            public string DisplayName { get; set; }  // Отображаемое имя (ФИО)
            public string Role { get; set; }         // Должность
            public string PhoneNumber { get; set; }  // Телефон
            public string NumberCard { get; set; }   // Номер карты

            // Полное представление для поиска и отображения
            public string SearchString => $"{DisplayName} - {NumberCard}";
        }

        public AddEditUserWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadStaffs();

            EditBtn.Visibility = Visibility.Collapsed;
            UserId.Visibility = Visibility.Collapsed;
            UserIdLabel.Visibility = Visibility.Collapsed;
        }

        public AddEditUserWindow(int userId)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadStaffs();
            _isEditMode = true;
            _userId = userId;
            AddBtn.Visibility = Visibility.Collapsed;
            EditBtn.Visibility = Visibility.Visible;
            UserId.Visibility = Visibility.Visible;
            UserIdLabel.Visibility = Visibility.Visible;
            UserId.Text = userId.ToString();
            LoadUserData(userId);
            Title.Text = "Редактирование пользователя";
        }

        private void LoadStaffs()
        {
            // Загружаем сотрудников с ролями "Системный администратор" или "Администратор стойки"
            var staffs = _dbContext.Staffs
                .Include("Persons")
                .Include("Roles")
                .Where(s => s.Roles.Name == "Системный администратор" || s.Roles.Name == "Администратор стойки")
                .ToList();

            // Если это режим добавления, исключаем сотрудников, у которых уже есть учетные записи
            if (!_isEditMode)
            {
                var staffsWithUsers = _dbContext.Users.Select(u => u.StaffID).ToList();
                staffs = staffs.Where(s => !staffsWithUsers.Contains(s.StaffID)).ToList();
            }

            _allStaffs = staffs.Select(s => new StaffInfo
            {
                StaffID = s.StaffID,
                DisplayName = $"{s.Persons.Surname} {s.Persons.Name} {s.Persons.MiddleName}",
                Role = s.Roles.Name,
                PhoneNumber = s.Persons.PhoneNumber,
                NumberCard = s.Persons.NumberCard ?? "Нет карты"
            }).ToList();
        }

        private void LoadUserData(int userId)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.UserID == userId);
            if (user != null)
            {
                var staffInfo = _allStaffs.FirstOrDefault(s => s.StaffID == user.StaffID);
                if (staffInfo == null)
                {
                    // Если данного сотрудника нет в списке (возможно из-за изменения роли),
                    // добавляем его для редактирования
                    var staff = _dbContext.Staffs
                        .Include("Persons")
                        .Include("Roles")
                        .FirstOrDefault(s => s.StaffID == user.StaffID);

                    if (staff != null)
                    {
                        staffInfo = new StaffInfo
                        {
                            StaffID = staff.StaffID,
                            DisplayName = $"{staff.Persons.Surname} {staff.Persons.Name} {staff.Persons.MiddleName}",
                            Role = staff.Roles.Name,
                            PhoneNumber = staff.Persons.PhoneNumber
                        };
                        _allStaffs.Add(staffInfo);
                    }
                }

                if (staffInfo != null)
                {
                    // Заполняем поля
                    StaffTextBox.Text = staffInfo.DisplayName;
                    SelectedStaffId.Text = staffInfo.StaffID.ToString();
                    RoleTextBox.Text = staffInfo.Role;
                    PhoneTextBox.Text = staffInfo.PhoneNumber;
                    LoginTextBox.Text = user.Login;
                }
            }
        }

        private void StaffTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = StaffTextBox.Text.ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                StaffsPopup.IsOpen = false;
                return;
            }

            var filteredStaffs = _allStaffs
                .Where(s =>
                    s.DisplayName.ToLower().Contains(searchText) ||
                    (s.NumberCard != null && s.NumberCard.ToLower().Contains(searchText))
                )
                .ToList();

            if (filteredStaffs.Any())
            {
                StaffsListBoxInPopup.ItemsSource = filteredStaffs;
                // Настраиваем отображение элементов в списке - и ФИО, и номер карты
                StaffsListBoxInPopup.DisplayMemberPath = "SearchString";
                StaffsListBoxInPopup.SelectedValuePath = "StaffID";
                StaffsPopup.IsOpen = true;
            }
            else
            {
                StaffsPopup.IsOpen = false;
            }
        }

        private void StaffsListBoxInPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StaffsListBoxInPopup.SelectedItem is StaffInfo selectedStaff)
            {
                StaffTextBox.Text = selectedStaff.SearchString;
                SelectedStaffId.Text = selectedStaff.StaffID.ToString();
                RoleTextBox.Text = selectedStaff.Role;
                PhoneTextBox.Text = selectedStaff.PhoneNumber;

                // Автоматически генерируем логин из ФИО, если это новый пользователь
                if (!_isEditMode && string.IsNullOrEmpty(LoginTextBox.Text))
                {
                    // Берем первую букву имени и фамилию полностью
                    string[] nameParts = selectedStaff.DisplayName.Split(' ');
                    if (nameParts.Length >= 2)
                    {
                        string firstInitial = nameParts[1].Length > 0 ? nameParts[1].Substring(0, 1).ToLower() : "";
                        string surname = nameParts[0].ToLower();
                        LoginTextBox.Text = firstInitial + surname;
                    }
                    // Если логин уже существует, добавляем номер карты как суффикс
                    string generatedLogin = LoginTextBox.Text;
                    if (_dbContext.Users.Any(u => u.Login == generatedLogin) && !string.IsNullOrEmpty(selectedStaff.NumberCard))
                    {
                        // Берем последние 4 символа номера карты, если они есть
                        string cardSuffix = selectedStaff.NumberCard.Length > 4 ?
                            selectedStaff.NumberCard.Substring(selectedStaff.NumberCard.Length - 4) :
                            selectedStaff.NumberCard;
                        LoginTextBox.Text = generatedLogin + cardSuffix;
                    }
                }

                StaffsPopup.IsOpen = false;
            }
        }

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(SelectedStaffId.Text))
            {
                MessageBox.Show("Пожалуйста, выберите сотрудника.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(LoginTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите логин.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isEditMode && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Пожалуйста, введите пароль.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int staffId = int.Parse(SelectedStaffId.Text);
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            // Проверка уникальности логина
            if (_dbContext.Users.Any(u => u.Login == login && (!_isEditMode || u.UserID != _userId)))
            {
                MessageBox.Show($"Пользователь с логином '{login}' уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isEditMode)
            {
                var user = _dbContext.Users.FirstOrDefault(u => u.UserID == _userId);
                if (user == null)
                {
                    MessageBox.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                user.StaffID = staffId;
                user.Login = login;

                // Обновляем пароль только если он был введен
                if (!string.IsNullOrWhiteSpace(password))
                {
                    user.Password = password;
                }

                _dbContext.SaveChanges();
                MessageBox.Show("Данные пользователя успешно обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            else
            {
                var user = new Users
                {
                    StaffID = staffId,
                    Login = login,
                    Password = password
                };

                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();

                MessageBox.Show("Пользователь успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}