using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FitnessCenterIS.Model;


namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private WindowState _previousWindowState;
        private string PasswordBox;
        private string _errorMessage;
        private string _adminName;

        public event PropertyChangedEventHandler PropertyChanged;
        public MainWindow()
        {
            InitializeComponent();
            Context();
        }
        public void Context()
        {
            DataContext = this;
        }
        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
        public string AdminName
        {
            get { return _adminName; }
            set
            {
                _adminName = value;
                OnPropertyChanged(nameof(AdminName));
            }
        }
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void TextBox_GotFocusLogin(object sender, RoutedEventArgs e)
        {
            if (LoginPlaceholder != null)
            {
                LoginPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void TextBox_LostFocusLogin(object sender, RoutedEventArgs e)
        {
            if (LoginTextBox != null && string.IsNullOrEmpty(LoginTextBox.Text))
            {
                LoginPlaceholder.Visibility = Visibility.Visible;
            }
        }
        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordTextBox.Password))
            {
                PasswordPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = (PasswordBox)sender;
            PasswordBox = passwordBox.Password;

            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordTextBox.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                var user1 = context.Users
                    .FirstOrDefault(u => u.Login == LoginTextBox.Text
                    && u.Password == PasswordBox);

                if (user1 != null)
                {
                    UserSession.CurrentAdmin = new UsersCollection
                    {
                        UserID = user1.UserID,
                        Name = user1.Staffs.Persons.Name,
                        Surname = user1.Staffs.Persons.Surname
                    };

                    if (user1.Staffs.RoleID == 1)
                    {
                        MenuWindow menuApplication = new MenuWindow();
                        menuApplication.Show();
                        Window mainWindow = Window.GetWindow(this);
                        mainWindow.Close();
                    }
                    else if(user1.Staffs.RoleID == 2)
                    {
                        MenuWindow menuApplication = new MenuWindow();
                        menuApplication.Personal.Visibility = Visibility.Collapsed;
                        menuApplication.Show();
                        Window mainWindow = Window.GetWindow(this);
                        mainWindow.Close();
                    }
                }
                else
                {
                    ErrorMessage = "Неверное имя пользователя или пароль.";
                }
            }
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = _previousWindowState;
                }
                else
                {
                    _previousWindowState = this.WindowState;
                    this.WindowState = WindowState.Maximized;
                }
            }
        }
    }
}
