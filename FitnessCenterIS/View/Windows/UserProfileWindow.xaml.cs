using System;
using System.Windows;
using System.Windows.Media.Imaging;
using FitnessCenterIS.Model;
using System.Linq;

namespace FitnessCenterIS.View.Windows
{
    public partial class UserProfileWindow : Window
    {
        private UsersCollection _currentUser;

        public UserProfileWindow()
        {
            InitializeComponent();

            // Регистрация эффекта тени, если он не определен в App.xaml
            if (!Application.Current.Resources.Contains("ShadowEffect"))
            {
                Application.Current.Resources.Add("ShadowEffect",
                    new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 15,
                        ShadowDepth = 1,
                        Direction = 270,
                        Color = System.Windows.Media.Color.FromArgb(50, 0, 0, 0),
                        Opacity = 0.3
                    });
            }
        }

        public UserProfileWindow(UsersCollection user) : this()
        {
            _currentUser = user;
            LoadUserData();
        }

        private void LoadUserData()
        {
            try
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    // Получаем пользователя с связанными данными
                    var user = context.Users.Find(_currentUser.UserID);

                    if (user != null && user.Staffs != null && user.Staffs.Persons != null)
                    {
                        var person = user.Staffs.Persons;
                        var staff = user.Staffs;
                        var role = staff.Roles;

                        // Создаем модель представления
                        DataContext = new UserProfileViewModel
                        {
                            FullName = $"{person.Surname} {person.Name} {(string.IsNullOrEmpty(person.MiddleName) ? "" : person.MiddleName)}".Trim(),
                            RoleName = role?.Name ?? "Роль не указана",
                            Email = person.Email ?? "Не указан",
                            PhoneNumber = person.PhoneNumber ?? "Не указан",
                            HireDate = staff.HireDate ?? "Не указана",
                            Address = person.Address ?? "Не указан",
                            ImagePerson = !string.IsNullOrEmpty(person.ImagePerson)
                                ? person.ImagePerson
                                : "pack://application:,,,/Assets/default_avatar.png"
                        };
                    }
                    else
                    {
                        MessageBox.Show("Не удалось загрузить данные пользователя.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // Реализация смены пароля (можно будет добавить отдельное окно)
            MessageBox.Show("Функция смены пароля будет реализована позднее.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Реализация выхода из системы
            var result = MessageBox.Show("Вы действительно хотите выйти из системы?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Очищаем сессию пользователя
                UserSession.CurrentAdmin = null;

                // Открываем окно входа
                var loginWindow = new MainWindow();
                loginWindow.Show();

                // Закрываем все текущие окна
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != loginWindow)
                        window.Close();
                }
            }
        }
    }

    // Модель представления для привязки данных
    public class UserProfileViewModel
    {
        public string FullName { get; set; }
        public string RoleName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string HireDate { get; set; }
        public string Address { get; set; }
        public string ImagePerson { get; set; }
    }
}
