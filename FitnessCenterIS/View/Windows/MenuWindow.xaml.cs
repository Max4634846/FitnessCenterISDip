using FitnessCenterIS.Model;
using FitnessCenterIS.View.Pages;
using FitnessCenterIS.View.Pages.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for MenuWindow.xaml
    /// </summary>
    public partial class MenuWindow : Window
    {
        private WindowState _previousWindowState;
        private SchedulePage _schedulePage;
        public MenuWindow()
        {
            InitializeComponent();
            MainFrameMain();
        }
        public void MainFrameMain()
        {
            MainPage dashboardView = new MainPage();
            MainFrame.Content = dashboardView;

            LoggedInAdminNameButton.Content = $"{UserSession.CurrentAdmin.Name} {UserSession.CurrentAdmin.Surname}";
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

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnClient_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ClientPage(this));
        }

        private void BtnMain_Click(object sender, RoutedEventArgs e)
        {
            MainFrameMain();
        }

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                PageTitleTextBlock.Text = page.Title;
            }
            else if (e.Content is UserControl userControl)
            {
                // UserControl не имеет свойства Title по умолчанию,
                // поэтому вам может потребоваться установить какое-то свое свойство
                // или использовать имя типа для отображения.
                PageTitleTextBlock.Text = userControl.GetType().Name.Replace("View", ""); // Пример: DashboardView -> Dashboard
            }
            else
            {
                PageTitleTextBlock.Text = ""; // Очищаем название, если загружено что-то другое
            }
        }

        private void BtnLead_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new LeadPage(this));
        }

        private void SeasonticketBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SeasonticketPage());

        }

        private void ServiceBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ServicesPage());
        }

        private void ScheduleDay_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SchedulePage(SchedulePage.ViewMode.Day));
        }

        private void ScheduleWeek_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SchedulePage(SchedulePage.ViewMode.Week));
        }

        private void ScheduleMonth_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SchedulePage(SchedulePage.ViewMode.Month));
        }

        private void WaitingList_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new WaitingListPage());
        }

        private void AttendanceHistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AttendanceHistoryPage());
        }

        private void LoggedInAdminNameButton_Click(object sender, RoutedEventArgs e)
        {
            UserProfileWindow userProfileWindow = new UserProfileWindow(UserSession.CurrentAdmin);
            userProfileWindow.ShowDialog();
        }

        private void Staff_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new StaffPage());
        }

        private void User_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new UserPage());
        }

        private void Role_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new RolesPage());
        }

        private void ManageGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new GroupsPage());
        }

        private void GroupClientsButton_Click(object sender, RoutedEventArgs e)
        {
            var groupClientsWindow = new GroupClientsWindow();
            groupClientsWindow.ShowDialog();
        }

        private void SalesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SalesPage());
        }

        private void AllAnalesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new DashboardReportPage());
        }

        private void FinancialBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new FinancialReportPage());
        }
    }
}
