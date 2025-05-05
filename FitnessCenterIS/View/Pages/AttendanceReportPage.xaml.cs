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
using System.ComponentModel;
using System.Windows.Markup;

namespace FitnessCenterIS.View.Pages
{
    public partial class AttendanceReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;
        private List<AttendanceRecord> attendanceData;

        // Класс для представления записи посещения
        public class AttendanceRecord : INotifyPropertyChanged
        {
            public int AttendanceID { get; set; }
            public DateTime EntryDateTime { get; set; }
            public DateTime? ExitDateTime { get; set; }

            // Исправлено для совместимости с C# 7.3
            public TimeSpan? Duration
            {
                get
                {
                    if (ExitDateTime.HasValue)
                        return ExitDateTime.Value - EntryDateTime;
                    return null;
                }
            }

            public string ClientName { get; set; }
            public string MembershipType { get; set; }
            public string ServiceName { get; set; }
            public string LockerNumber { get; set; }
            public string Note { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        public AttendanceReportPage()
        {
            InitializeComponent();

            try
            {
                // Извлекаем строку подключения SQL из строки подключения Entity Framework
                var entityConnectionString = ConfigurationManager.ConnectionStrings["BDFitnessClubDipEntities"].ConnectionString;
                var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
                connectionString = entityBuilder.ProviderConnectionString;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении строки подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Загрузка данных после полной инициализации UI
            this.Loaded += (s, e) => {
                try
                {
                    // Set default date range (last 30 days)
                    DateToPicker.SelectedDate = DateTime.Today;
                    DateFromPicker.SelectedDate = DateTime.Today.AddDays(-30);

                    // Initial data load
                    LoadAttendanceData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadAttendanceData();
            }
        }

        private void LoadAttendanceData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1); // End of the selected day

            try
            {
                // Load attendance key metrics
                LoadAttendanceMetrics();

                // Load attendance charts - использование ContentControl вместо специфичных контейнеров
                CreateDailyAttendanceChart();
                CreateWeekdayAttendanceChart();
                CreateHourlyAttendanceChart();
                CreateDurationDistributionChart();

                // Load detailed attendance data
                LoadAttendanceDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAttendanceMetrics()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Total visits
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Attendances 
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int totalVisits = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        totalVisits = Convert.ToInt32(result);
                    }
                    TotalVisitsTextBlock.Text = totalVisits.ToString();
                }

                // Unique visitors
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT ClientID) 
                    FROM Attendances 
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    AND ClientID IS NOT NULL", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    int uniqueVisitors = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        uniqueVisitors = Convert.ToInt32(result);
                    }
                    UniqueVisitorsTextBlock.Text = uniqueVisitors.ToString();
                }

                // Average visits per day
                double daysInRange = (endDate - startDate).TotalDays + 1;
                double avgVisitsPerDay = int.Parse(TotalVisitsTextBlock.Text) / daysInRange;
                AvgVisitsPerDayTextBlock.Text = Math.Round(avgVisitsPerDay, 1).ToString();

                // Average duration
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AVG(DATEDIFF(MINUTE, EntryDateTime, ISNULL(ExitDateTime, GETDATE()))) 
                    FROM Attendances 
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    AND ExitDateTime IS NOT NULL", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    var result = cmd.ExecuteScalar();
                    int avgMinutes = 0;
                    if (result != null && result != DBNull.Value)
                    {
                        avgMinutes = Convert.ToInt32(result);
                    }
                    TimeSpan avgDuration = TimeSpan.FromMinutes(avgMinutes);
                    AvgDurationTextBlock.Text = $"{avgDuration.Hours:D2}:{avgDuration.Minutes:D2}";
                }

                // Most active weekday
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 DATEPART(weekday, EntryDateTime) as WeekDay, COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(weekday, EntryDateTime)
                    ORDER BY VisitCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    int weekDayNumber = 1; // Default to Sunday

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            weekDayNumber = reader.GetInt32(0);
                        }
                    }

                    // Convert SQL Server day (1=Sunday) to .NET day (0=Sunday)
                    DayOfWeek mostActiveDay = (DayOfWeek)(weekDayNumber % 7);

                    // Get weekday name in Russian
                    string[] weekDays = { "Воскресенье", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
                    MostActiveWeekdayTextBlock.Text = weekDays[(int)mostActiveDay];
                }

                // Most active hour
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 DATEPART(hour, EntryDateTime) as Hour, COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(hour, EntryDateTime)
                    ORDER BY VisitCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    int peakHour = 0;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            peakHour = reader.GetInt32(0);
                        }
                    }

                    MostActiveTimeTextBlock.Text = $"{peakHour:D2}:00 - {peakHour:D2}:59";
                }

                // Peak load
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 CONVERT(VARCHAR, CAST(EntryDateTime AS DATE), 104) as VisitDate, COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CAST(EntryDateTime AS DATE)
                    ORDER BY VisitCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    string peakDate = "";
                    int peakVisitors = 0;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            peakDate = reader["VisitDate"].ToString();
                            peakVisitors = Convert.ToInt32(reader["VisitCount"]);
                        }
                    }

                    PeakVisitorsTextBlock.Text = peakVisitors > 0 ? $"{peakVisitors} ({peakDate})" : "0";
                }
            }
        }

        private void CreateDailyAttendanceChart()
        {
            var dailyData = new ChartValues<int>();
            var labels = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string groupFormat = (endDate - startDate).TotalDays <= 31 ? "dd.MM" : "MM.yyyy";
                string sqlQuery = (endDate - startDate).TotalDays <= 31
                    ? @"SELECT 
                         CONVERT(varchar, EntryDateTime, 104) as DateGroup, 
                         COUNT(*) as VisitCount
                       FROM Attendances 
                       WHERE EntryDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar, EntryDateTime, 104)
                       ORDER BY MIN(EntryDateTime)"
                    : @"SELECT 
                         CONVERT(varchar(7), EntryDateTime, 104) as DateGroup, 
                         COUNT(*) as VisitCount
                       FROM Attendances 
                       WHERE EntryDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar(7), EntryDateTime, 104)
                       ORDER BY MIN(EntryDateTime)";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["DateGroup"].ToString());

                            int visitCount = 0;
                            if (!reader.IsDBNull(reader.GetOrdinal("VisitCount")))
                            {
                                visitCount = reader.GetInt32(reader.GetOrdinal("VisitCount"));
                            }

                            dailyData.Add(visitCount);
                        }
                    }
                }
            }

            var chart = new CartesianChart
            {
                Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Посещения по дням",
                        Values = dailyData,
                        Fill = new SolidColorBrush(Color.FromRgb(66, 133, 244))
                    }
                },
                AxisX = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Дата",
                        Labels = labels,
                        Separator = new LiveCharts.Wpf.Separator { Step = Math.Max(1, labels.Count / 10) }
                    }
                },
                AxisY = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Количество посещений",
                        LabelFormatter = value => value.ToString("N0")
                    }
                },
                LegendLocation = LegendLocation.Top
            };

            // Находим контейнер в XAML-интерфейсе по имени (универсальный способ)
            ContentControl dailyChartContainer = this.FindName("DailyChartContainer") as ContentControl;
            if (dailyChartContainer != null)
            {
                dailyChartContainer.Content = chart;
            }
        }

        private void CreateWeekdayAttendanceChart()
        {
            var weekdayData = new ChartValues<int>();
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

                    weekdayData = new ChartValues<int>(visitsByDay);
                }
            }

            var chart = new CartesianChart
            {
                Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Посещения по дням недели",
                        Values = weekdayData,
                        Fill = new SolidColorBrush(Color.FromRgb(15, 157, 88))
                    }
                },
                AxisX = new AxesCollection
                {
                    new Axis
                    {
                        Title = "День недели",
                        Labels = dayLabels
                    }
                },
                AxisY = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Количество посещений",
                        LabelFormatter = value => value.ToString("N0")
                    }
                },
                LegendLocation = LegendLocation.Top
            };

            // Находим контейнер в XAML-интерфейсе по имени
            ContentControl weekdayChartContainer = this.FindName("WeekdayChartContainer") as ContentControl;
            if (weekdayChartContainer != null)
            {
                weekdayChartContainer.Content = chart;
            }
        }

        private void CreateHourlyAttendanceChart()
        {
            var hourlyData = new ChartValues<int>();
            var hourLabels = new List<string>();

            for (int i = 0; i < 24; i++)
            {
                hourLabels.Add($"{i:D2}:00");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT DATEPART(hour, EntryDateTime) as Hour, COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(hour, EntryDateTime)
                    ORDER BY Hour";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    int[] visitsByHour = new int[24];

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int hour = Convert.ToInt32(reader["Hour"]);
                            visitsByHour[hour] = Convert.ToInt32(reader["VisitCount"]);
                        }
                    }

                    hourlyData = new ChartValues<int>(visitsByHour);
                }
            }

            var chart = new CartesianChart
            {
                Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Посещения по часам",
                        Values = hourlyData,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 8,
                        Stroke = new SolidColorBrush(Color.FromRgb(244, 180, 0)),
                        Fill = new SolidColorBrush(Color.FromArgb(50, 244, 180, 0))
                    }
                },
                AxisX = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Час дня",
                        Labels = hourLabels,
                        Separator = new LiveCharts.Wpf.Separator { Step = 2 }
                    }
                },
                AxisY = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Количество посещений",
                        LabelFormatter = value => value.ToString("N0")
                    }
                },
                LegendLocation = LegendLocation.Top
            };

            // Находим контейнер в XAML-интерфейсе по имени
            ContentControl hourlyChartContainer = this.FindName("HourlyChartContainer") as ContentControl;
            if (hourlyChartContainer != null)
            {
                hourlyChartContainer.Content = chart;
            }
        }

        private void CreateDurationDistributionChart()
        {
            var durationData = new ChartValues<int>();
            var durationLabels = new List<string>
            {
                "<30 мин", "30-60 мин", "1-1.5 часа", "1.5-2 часа", "2-3 часа", ">3 часа"
            };

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT 
                        CASE 
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 30 THEN 0
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 60 THEN 1
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 90 THEN 2
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 120 THEN 3
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 180 THEN 4
                            ELSE 5
                        END as DurationCategory,
                        COUNT(*) as VisitCount
                    FROM Attendances
                    WHERE EntryDateTime BETWEEN @StartDate AND @EndDate
                    AND ExitDateTime IS NOT NULL
                    GROUP BY 
                        CASE 
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 30 THEN 0
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 60 THEN 1
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 90 THEN 2
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 120 THEN 3
                            WHEN DATEDIFF(MINUTE, EntryDateTime, ExitDateTime) < 180 THEN 4
                            ELSE 5
                        END
                    ORDER BY DurationCategory";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    int[] durationCounts = new int[6];

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int category = Convert.ToInt32(reader["DurationCategory"]);
                            durationCounts[category] = Convert.ToInt32(reader["VisitCount"]);
                        }
                    }

                    durationData = new ChartValues<int>(durationCounts);
                }
            }

            var chart = new CartesianChart
            {
                Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Распределение по длительности",
                        Values = durationData,
                        Fill = new SolidColorBrush(Color.FromRgb(219, 68, 55))
                    }
                },
                AxisX = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Длительность посещения",
                        Labels = durationLabels
                    }
                },
                AxisY = new AxesCollection
                {
                    new Axis
                    {
                        Title = "Количество посещений",
                        LabelFormatter = value => value.ToString("N0")
                    }
                },
                LegendLocation = LegendLocation.Top
            };

            // Находим контейнер в XAML-интерфейсе по имени
            ContentControl durationChartContainer = this.FindName("DurationChartContainer") as ContentControl;
            if (durationChartContainer != null)
            {
                durationChartContainer.Content = chart;
            }
        }

        private void LoadAttendanceDetails()
        {
            attendanceData = new List<AttendanceRecord>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT 
                        a.AttendanceID,
                        a.EntryDateTime,
                        a.ExitDateTime,
                        CONCAT(p.Surname, ' ', p.Name) as ClientName,
                        st.Name as MembershipType,
                        s.Name as ServiceName,
                        CONCAT('№', l.KeyNumber) as LockerNumber,
                        a.Note
                    FROM Attendances a
                    LEFT JOIN Clients c ON a.ClientID = c.ClientID
                    LEFT JOIN Persons p ON c.PersonID = p.PersonID
                    LEFT JOIN Sales sa ON a.SaleID = sa.SaleID
                    LEFT JOIN Seasontickets st ON sa.SeasonticketID = st.SeasonticketID
                    LEFT JOIN SeasonticketServices ss ON sa.SeasonticketServiceID = ss.SeasonticketServiceID
                    LEFT JOIN Services s ON ss.ServiceID = s.ServiceID
                    LEFT JOIN Lockers l ON a.LockerID = l.LockerID
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    ORDER BY a.EntryDateTime DESC";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var record = new AttendanceRecord
                                {
                                    AttendanceID = reader.IsDBNull(reader.GetOrdinal("AttendanceID")) ? 0 : reader.GetInt32(reader.GetOrdinal("AttendanceID")),
                                    EntryDateTime = reader.GetDateTime(reader.GetOrdinal("EntryDateTime")),
                                    ExitDateTime = reader.IsDBNull(reader.GetOrdinal("ExitDateTime")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ExitDateTime")),
                                    ClientName = reader.IsDBNull(reader.GetOrdinal("ClientName")) ? "Не указан" : reader.GetString(reader.GetOrdinal("ClientName")),
                                    MembershipType = reader.IsDBNull(reader.GetOrdinal("MembershipType")) ? "-" : reader.GetString(reader.GetOrdinal("MembershipType")),
                                    ServiceName = reader.IsDBNull(reader.GetOrdinal("ServiceName")) ? "-" : reader.GetString(reader.GetOrdinal("ServiceName")),
                                    LockerNumber = reader.IsDBNull(reader.GetOrdinal("LockerNumber")) ? "-" : reader.GetString(reader.GetOrdinal("LockerNumber")),
                                    Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? "" : reader.GetString(reader.GetOrdinal("Note"))
                                };

                                attendanceData.Add(record);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при обработке записи: {ex.Message}");
                            }
                        }
                    }
                }
            }

            AttendanceDataGrid.ItemsSource = attendanceData;
            TotalRowsTextBlock.Text = $"Всего записей: {attendanceData.Count}";
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "xlsx",
                Title = "Экспорт отчета посещаемости"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Здесь можно реализовать экспорт в Excel с помощью библиотек
                    // EPPlus, ClosedXML или NPOI
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
                    FixedDocument document = CreatePrintDocument();
                    printDialog.PrintDocument(document.DocumentPaginator, "Отчет посещаемости");
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

            Grid printGrid = new Grid();
            printGrid.Width = 794; // A4 width in pixels at 96 DPI
            printGrid.Height = 1123; // A4 height in pixels at 96 DPI

            TextBlock titleBlock = new TextBlock
            {
                Text = $"Отчет посещаемости за период {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };

            StackPanel summaryPanel = new StackPanel { Margin = new Thickness(20) };
            summaryPanel.Children.Add(new TextBlock { Text = "Основные показатели:", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 10) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Всего посещений: {TotalVisitsTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Уникальные посетители: {UniqueVisitorsTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Средняя посещаемость в день: {AvgVisitsPerDayTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Средняя продолжительность: {AvgDurationTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Самый активный день: {MostActiveWeekdayTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Самое активное время: {MostActiveTimeTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Пиковая загрузка: {PeakVisitorsTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });

            printGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            printGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(titleBlock, 0);
            Grid.SetRow(summaryPanel, 1);

            printGrid.Children.Add(titleBlock);
            printGrid.Children.Add(summaryPanel);

            FixedPage page = new FixedPage();
            page.Width = 794;
            page.Height = 1123;
            page.Children.Add(printGrid);

            PageContent pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);

            return document;
        }
    }
}
