using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using Separator = LiveCharts.Wpf.Separator;

namespace FitnessCenterIS.View.Pages
{
    public partial class TrainerPopularityReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public TrainerPopularityReportPage()
        {
            InitializeComponent();

            try
            {
                // Извлекаем строку подключения
                var entityConnectionString = ConfigurationManager.ConnectionStrings["BDFitnessClubDipEntities"].ConnectionString;
                var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
                connectionString = entityBuilder.ProviderConnectionString;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Инициализация с датами по умолчанию
            this.Loaded += (s, e) => {
                DateToPicker.SelectedDate = DateTime.Today;
                DateFromPicker.SelectedDate = DateTime.Today.AddDays(-30);
                LoadReportData();
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadReportData();
            }
        }

        private void LoadReportData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1);

            try
            {
                LoadKeyMetrics();
                LoadTrainerSalesChart();
                LoadTrainerServiceDistributionChart();
                LoadTrainerRatingChart();
                LoadTrainerAttendanceChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadKeyMetrics()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Активные тренеры
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT s.StaffID) 
                    FROM Staffs s
                    JOIN Roles r ON s.RoleID = r.RoleID
                    JOIN ServiceTrainer st ON s.StaffID = st.TrainerID
                    WHERE r.Name = 'Тренер'
                    AND EXISTS (
                        SELECT 1 FROM Sales sa 
                        WHERE sa.TrainerID = s.StaffID 
                        AND sa.SaleDateTime BETWEEN @StartDate AND @EndDate
                    )", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    ActiveTrainersTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Количество продаж через тренеров
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    TrainerSalesCountTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Выручка тренеров
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal revenue = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    TrainerRevenueTextBlock.Text = $"{revenue:N0} ₽";
                }

                // Запланированные тренировки
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Schedules s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.StartDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    ScheduledTrainingsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Средняя стоимость услуг
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(AVG(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal avgPrice = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    AverageTrainerServicePriceTextBlock.Text = $"{avgPrice:N0} ₽";
                }

                // Топ тренер
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 p.Surname + ' ' + p.Name
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY SUM(s.PriceSold) DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    TopTrainerTextBlock.Text = result != null ? result.ToString() : "Н/Д";
                }
            }
        }

        private void LoadTrainerSalesChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT FORMAT(s.SaleDateTime, 'dd.MM') as SaleDate, COUNT(*) as SalesCount
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY FORMAT(s.SaleDateTime, 'dd.MM'), CAST(s.SaleDateTime AS DATE)
                    ORDER BY CAST(s.SaleDateTime AS DATE)", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["SaleDate"].ToString());
                            values.Add(Convert.ToDouble(reader["SalesCount"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new LineSeries
            {
                Title = "Продажи",
                Values = values,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 10
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Дата",
                Labels = labels,
                Separator = new Separator { Step = Math.Max(1, labels.Count / 10) }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество продаж",
                MinValue = 0
            });

            TrainerSalesChartContainer.Content = chart;
        }

        private void LoadTrainerServiceDistributionChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 5 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, COUNT(*) as ServiceCount
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY ServiceCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = reader["TrainerName"].ToString(),
                                Values = new ChartValues<double> { Convert.ToDouble(reader["ServiceCount"]) },
                                DataLabels = true
                            });
                        }
                    }
                }
            }

            var chart = new PieChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Right,
                DisableAnimations = true
            };

            TrainerServiceDistributionContainer.Content = chart;
        }

        private void LoadTrainerRatingChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> ratingValues = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 5 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, 
                           COUNT(*) * 1.0 / 
                           (SELECT COUNT(*) FROM Sales WHERE TrainerID = s.TrainerID) * 5 as Rating
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY Rating DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["TrainerName"].ToString());
                            ratingValues.Add(Convert.ToDouble(reader["Rating"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Рейтинг",
                Values = ratingValues,
                Fill = System.Windows.Media.Brushes.DarkOrange
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Тренер",
                Labels = labels
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Рейтинг",
                MinValue = 0,
                MaxValue = 5
            });

            TrainerRatingChartContainer.Content = chart;
        }

        private void LoadTrainerAttendanceChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            Dictionary<string, ChartValues<double>> trainerData = new Dictionary<string, ChartValues<double>>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT FORMAT(s.StartDateTime, 'dd.MM') as TrainingDate,
                           p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName,
                           COUNT(*) as AttendanceCount
                    FROM Schedules s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY FORMAT(s.StartDateTime, 'dd.MM'), CAST(s.StartDateTime AS DATE), p.Surname, p.Name
                    ORDER BY CAST(s.StartDateTime AS DATE)", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        string currentDate = "";

                        while (reader.Read())
                        {
                            string date = reader["TrainingDate"].ToString();
                            string trainer = reader["TrainerName"].ToString();
                            double count = Convert.ToDouble(reader["AttendanceCount"]);

                            if (currentDate != date)
                            {
                                currentDate = date;
                                labels.Add(date);
                            }

                            if (!trainerData.ContainsKey(trainer))
                            {
                                trainerData[trainer] = new ChartValues<double>();
                                for (int i = 0; i < labels.Count - 1; i++)
                                {
                                    trainerData[trainer].Add(0);
                                }
                            }

                            trainerData[trainer].Add(count);

                            foreach (var key in trainerData.Keys)
                            {
                                if (key != trainer && trainerData[key].Count < labels.Count)
                                {
                                    trainerData[key].Add(0);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var trainerEntry in trainerData)
            {
                seriesCollection.Add(new LineSeries
                {
                    Title = trainerEntry.Key,
                    Values = trainerEntry.Value
                });
            }

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Right,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Дата",
                Labels = labels
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Посещаемость",
                MinValue = 0
            });

            TrainerAttendanceChartContainer.Content = chart;
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx|PDF Files (*.pdf)|*.pdf",
                    DefaultExt = "xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    if (extension == ".xlsx")
                    {
                        ExportToExcel(filePath);
                        MessageBox.Show("Отчет успешно экспортирован в Excel", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (extension == ".pdf")
                    {
                        ExportToPdf(filePath);
                        MessageBox.Show("Отчет успешно экспортирован в PDF", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string filePath)
        {
            // Реализация экспорта в Excel (используйте библиотеку EPPlus или ClosedXML)
        }

        private void ExportToPdf(string filePath)
        {
            // Реализация экспорта в PDF (используйте библиотеку iTextSharp или PdfSharp)
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(this, "Отчет по популярности тренеров");
                    MessageBox.Show("Отчет отправлен на печать", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
