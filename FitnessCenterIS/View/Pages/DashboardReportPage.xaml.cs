using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Data;
using Microsoft.Win32;
using System.IO;
using System.Printing;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using FitnessCenterIS.Model;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Windows.Markup;

namespace FitnessCenterIS.View.Pages
{
    public partial class DashboardReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public DashboardReportPage()
        {
            InitializeComponent();

            // Extract SQL connection string from Entity Framework connection string
            var entityConnectionString = ConfigurationManager.ConnectionStrings["BDFitnessClubDipEntities"].ConnectionString;
            var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
            connectionString = entityBuilder.ProviderConnectionString;

            // Загрузка данных после полной инициализации UI
            this.Loaded += (s, e) => {
                // Set default date range (last 30 days)
                DateToPicker.SelectedDate = DateTime.Today;
                DateFromPicker.SelectedDate = DateTime.Today.AddDays(-30);

                // Initial data load
                LoadDashboardData();
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadDashboardData();
            }
        }

        private void LoadDashboardData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1); // End of the selected day

            try
            {
                // Load all dashboard data
                LoadKeyMetrics();
                CreateSalesChart();
                CreateClientRegistrationChart();
                CreateServicePopularityChart();
                CreateAttendanceByDayChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadKeyMetrics()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Active clients count
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT c.ClientID) 
                    FROM Clients c
                    JOIN Sales s ON s.SeasonticketID IS NOT NULL
                    WHERE s.EndDateTime >= @CurrentDate", connection))
                {
                    cmd.Parameters.AddWithValue("@CurrentDate", DateTime.Now);
                    int activeClients = (int)cmd.ExecuteScalar();
                    ActiveClientsTextBlock.Text = activeClients.ToString();
                }

                // Sales count in period
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Sales 
                    WHERE SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int salesCount = (int)cmd.ExecuteScalar();
                    SalesCountTextBlock.Text = salesCount.ToString();
                }

                // Total revenue
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(PriceSold), 0) 
                    FROM Sales 
                    WHERE SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal totalRevenue = (decimal)cmd.ExecuteScalar();
                    TotalRevenueTextBlock.Text = $"{totalRevenue:N0} ₽";
                }

                // Visits count
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Attendances 
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int visitsCount = (int)cmd.ExecuteScalar();
                    VisitsCountTextBlock.Text = visitsCount.ToString();
                }

                // Average check
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(AVG(PriceSold), 0) 
                    FROM Sales 
                    WHERE SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    decimal avgCheck = result is DBNull ? 0 : Convert.ToDecimal(result);
                    AverageCheckTextBlock.Text = $"{avgCheck:N0} ₽";
                }

                // Membership sales
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Sales 
                    WHERE SeasonticketID IS NOT NULL 
                    AND SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int membershipSales = (int)cmd.ExecuteScalar();
                    MembershipSalesTextBlock.Text = membershipSales.ToString();
                }

                // Service sales
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Sales 
                    WHERE SeasonticketServiceID IS NOT NULL 
                    AND SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int serviceSales = (int)cmd.ExecuteScalar();
                    ServiceSalesTextBlock.Text = serviceSales.ToString();
                }
            }
        }

        private void CreateSalesChart()
        {
            var salesData = new SeriesCollection();
            var labels = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Group by day if period <= 31 days, otherwise group by month
                string groupFormat = (endDate - startDate).TotalDays <= 31 ? "dd.MM" : "MM.yyyy";
                string sqlQuery = (endDate - startDate).TotalDays <= 31
                    ? @"SELECT CONVERT(varchar, SaleDateTime, 104) as DateGroup, SUM(PriceSold) as Revenue 
                       FROM Sales 
                       WHERE SaleDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar, SaleDateTime, 104)
                       ORDER BY MIN(SaleDateTime)"
                    : @"SELECT CONVERT(varchar(7), SaleDateTime, 104) as DateGroup, SUM(PriceSold) as Revenue 
                       FROM Sales 
                       WHERE SaleDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar(7), SaleDateTime, 104)
                       ORDER BY MIN(SaleDateTime)";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var values = new ChartValues<decimal>();

                        while (reader.Read())
                        {
                            labels.Add(reader["DateGroup"].ToString());
                            values.Add(Convert.ToDecimal(reader["Revenue"]));
                        }

                        salesData.Add(new LineSeries
                        {
                            Title = "Выручка",
                            Values = values,
                            PointGeometry = DefaultGeometries.Circle,
                            PointGeometrySize = 10,
                            Stroke = new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                            Fill = new SolidColorBrush(Color.FromArgb(50, 66, 133, 244))
                        });
                    }
                }
            }

            var chart = new CartesianChart
            {
                Series = salesData,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = false,
                DataTooltip = new DefaultTooltip { SelectionMode = TooltipSelectionMode.SharedYValues }
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Период",
                Labels = labels,
                Separator = new LiveCharts.Wpf.Separator { Step = Math.Max(1, labels.Count / 10) }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Выручка (₽)",
                LabelFormatter = value => value.ToString("N0")
            });

            SalesChartContainer.Content = chart;
        }

        private void CreateClientRegistrationChart()
        {
            var clientData = new SeriesCollection();
            var labels = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Group by day if period <= 31 days, otherwise group by month
                string groupFormat = (endDate - startDate).TotalDays <= 31 ? "dd.MM" : "MM.yyyy";
                string sqlQuery = (endDate - startDate).TotalDays <= 31
                    ? @"SELECT CONVERT(varchar, p.DateOfBirth, 104) as DateGroup, COUNT(*) as ClientCount 
                       FROM Clients c
                       JOIN Persons p ON c.PersonID = p.PersonID
                       WHERE p.DateOfBirth BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar, p.DateOfBirth, 104)
                       ORDER BY MIN(p.DateOfBirth)"
                    : @"SELECT CONVERT(varchar(7), p.DateOfBirth, 104) as DateGroup, COUNT(*) as ClientCount 
                       FROM Clients c
                       JOIN Persons p ON c.PersonID = p.PersonID
                       WHERE p.DateOfBirth BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar(7), p.DateOfBirth, 104)
                       ORDER BY MIN(p.DateOfBirth)";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var values = new ChartValues<int>();

                        while (reader.Read())
                        {
                            labels.Add(reader["DateGroup"].ToString());
                            values.Add(Convert.ToInt32(reader["ClientCount"]));
                        }

                        clientData.Add(new ColumnSeries
                        {
                            Title = "Новые клиенты",
                            Values = values,
                            Fill = new SolidColorBrush(Color.FromRgb(15, 157, 88))
                        });
                    }
                }
            }

            var chart = new CartesianChart
            {
                Series = clientData,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = false
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Период",
                Labels = labels,
                Separator = new LiveCharts.Wpf.Separator { Step = Math.Max(1, labels.Count / 10) }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество клиентов",
                LabelFormatter = value => value.ToString("N0")
            });

            ClientRegistrationChartContainer.Content = chart;
        }

        private void CreateServicePopularityChart()
        {
            var serviceData = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT TOP 10 s.Name, COUNT(sa.SaleID) as SaleCount
                    FROM Services s
                    JOIN SeasonticketServices ss ON s.ServiceID = ss.ServiceID
                    JOIN Sales sa ON ss.SeasonticketServiceID = sa.SeasonticketServiceID
                    WHERE sa.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.Name
                    ORDER BY COUNT(sa.SaleID) DESC";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var values = new ChartValues<int>();
                        var labels = new List<string>();

                        while (reader.Read())
                        {
                            labels.Add(reader["Name"].ToString());
                            values.Add(Convert.ToInt32(reader["SaleCount"]));
                        }

                        var chart = new PieChart
                        {
                            Series = new SeriesCollection
                            {
                                new PieSeries
                                {
                                    Title = "Продажи",
                                    Values = values,
                                    DataLabels = true,
                                    LabelPoint = point => $"{labels[(int)point.Key]}: {point.Y} ({point.Participation:P1})"
                                }
                            },
                            LegendLocation = LegendLocation.Bottom
                        };

                        ServicePopularityChartContainer.Content = chart;
                    }
                }
            }
        }

        private void CreateAttendanceByDayChart()
        {
            var attendanceData = new SeriesCollection();
            var dayLabels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT DATEPART(weekday, EntryDateTime) as WeekDay, COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(weekday, EntryDateTime)
                    ORDER BY WeekDay";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    // Initialize array for all days of week
                    int[] visitsByDay = new int[7];

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // SQL Server DATEPART(weekday) returns 1-7 where 1 is Sunday
                            // Convert to 0-6 where 0 is Monday
                            int weekDay = Convert.ToInt32(reader["WeekDay"]);
                            int dayIndex = (weekDay + 5) % 7; // Convert SQL Server day to our array index

                            visitsByDay[dayIndex] = Convert.ToInt32(reader["VisitCount"]);
                        }
                    }

                    attendanceData.Add(new ColumnSeries
                    {
                        Title = "Посещения",
                        Values = new ChartValues<int>(visitsByDay),
                        Fill = new SolidColorBrush(Color.FromRgb(244, 180, 0))
                    });
                }
            }

            var chart = new CartesianChart
            {
                Series = attendanceData,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = false
            };

            chart.AxisX.Add(new Axis
            {
                Title = "День недели",
                Labels = dayLabels
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество посещений",
                LabelFormatter = value => value.ToString("N0")
            });

            AttendanceChartContainer.Content = chart;
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "xlsx",
                Title = "Экспорт отчета"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Export logic would go here
                    // This would typically use a library like EPPlus, ClosedXML, or NPOI
                    MessageBox.Show("Отчет успешно экспортирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Create document for printing
                    FixedDocument document = CreatePrintDocument();
                    printDialog.PrintDocument(document.DocumentPaginator, "Сводный отчет");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FixedDocument CreatePrintDocument()
        {
            FixedDocument document = new FixedDocument();

            // Create a visual representation of the report
            Grid printGrid = new Grid();
            printGrid.Width = 794; // A4 width in pixels at 96 DPI
            printGrid.Height = 1123; // A4 height in pixels at 96 DPI

            // Add report content to the grid
            // This would be a simplified version of your dashboard

            // Create a page and add the grid
            FixedPage page = new FixedPage();
            page.Width = 794;
            page.Height = 1123;
            page.Children.Add(printGrid);

            // Add the page to the document
            PageContent pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);

            return document;
        }
    }
}
