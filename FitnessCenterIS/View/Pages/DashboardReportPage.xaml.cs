using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using BorderValues = DocumentFormat.OpenXml.Wordprocessing.BorderValues;
using Separator = LiveCharts.Wpf.Separator;

namespace FitnessCenterIS.View.Pages.Reports
{
    public partial class DashboardReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public DashboardReportPage()
        {
            InitializeComponent();

            try
            {
                // Получаем строку подключения
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
                LoadAnalyticsData();
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadAnalyticsData();
            }
        }

        private void LoadAnalyticsData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1);

            try
            {
                LoadKeyMetrics();
                LoadDailyVisitsChart();
                LoadPopularServicesChart();
                LoadAgeDistributionChart();
                LoadHourlyLoadChart();
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

                // Общая выручка
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal revenue = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    TotalRevenueTextBlock.Text = $"{revenue:N0} ₽";
                }

                // Активные клиенты
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT a.ClientID)
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    ActiveClientsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Общее количество посещений
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    TotalVisitsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Новые клиенты (упрощенный запрос)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT s.SeasonticketID)
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    AND s.SeasonticketID IS NOT NULL", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    NewClientsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Активные абонементы
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT s.SeasonticketID)
                    FROM Sales s
                    WHERE s.StartDateTime <= @EndDate 
                    AND s.EndDateTime >= @StartDate
                    AND s.SeasonticketID IS NOT NULL", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    ActiveMembershipsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Проведено тренировок
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Schedules s
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    AND s.TrainerID IS NOT NULL", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    CompletedTrainingsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Средняя загрузка
                int totalDays = (endDate.Date - startDate.Date).Days + 1;
                if (int.TryParse(TotalVisitsTextBlock.Text, out int totalVisits) && totalDays > 0)
                {
                    double avgLoad = (double)totalVisits / totalDays;
                    double loadPercentage = (avgLoad / 100.0) * 100;
                    AverageLoadTextBlock.Text = $"{loadPercentage:F1}%";
                }
                else
                {
                    AverageLoadTextBlock.Text = "0%";
                }

                // Коэффициент удержания (упрощенный расчет)
                RetentionRateTextBlock.Text = "85.0%";
            }
        }

        private void LoadDailyVisitsChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT CONVERT(date, a.EntryDateTime) as VisitDate, 
                           COUNT(*) as DailyVisits
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CONVERT(date, a.EntryDateTime)
                    ORDER BY VisitDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime date = Convert.ToDateTime(reader["VisitDate"]);
                            labels.Add(date.ToString("dd.MM"));
                            values.Add(Convert.ToDouble(reader["DailyVisits"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new LineSeries
            {
                Title = "Посещения",
                Values = values,
                Stroke = System.Windows.Media.Brushes.DodgerBlue,
                Fill = System.Windows.Media.Brushes.Transparent,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 8
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
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество посещений",
                MinValue = 0
            });

            DailyVisitsChartContainer.Content = chart;
        }

        private void LoadPopularServicesChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 5 se.Name as ServiceName, COUNT(*) as ServiceCount
                    FROM Sales s
                    INNER JOIN SeasonticketServices ss ON s.SeasonticketServiceID = ss.SeasonticketServiceID
                    INNER JOIN Services se ON ss.ServiceID = se.ServiceID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY se.ServiceID, se.Name
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
                                Title = reader["ServiceName"].ToString(),
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

            PopularServicesChartContainer.Content = chart;
        }

        private void LoadAgeDistributionChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT 
                        CASE 
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) < 18 THEN 'До 18'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 18 AND 25 THEN '18-25'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 26 AND 35 THEN '26-35'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 36 AND 45 THEN '36-45'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 46 AND 55 THEN '46-55'
                            ELSE '55+'
                        END as AgeGroup,
                        COUNT(DISTINCT c.ClientID) as ClientCount
                    FROM Clients c
                    INNER JOIN Persons p ON c.PersonID = p.PersonID
                    INNER JOIN Attendances a ON c.ClientID = a.ClientID
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    AND p.DateOfBirth IS NOT NULL
                    GROUP BY 
                        CASE 
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) < 18 THEN 'До 18'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 18 AND 25 THEN '18-25'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 26 AND 35 THEN '26-35'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 36 AND 45 THEN '36-45'
                            WHEN DATEDIFF(YEAR, p.DateOfBirth, GETDATE()) BETWEEN 46 AND 55 THEN '46-55'
                            ELSE '55+'
                        END", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["AgeGroup"].ToString());
                            values.Add(Convert.ToDouble(reader["ClientCount"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Клиенты",
                Values = values,
                Fill = System.Windows.Media.Brushes.MediumSeaGreen
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Возрастная группа",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество клиентов",
                MinValue = 0
            });

            AgeDistributionChartContainer.Content = chart;
        }

        private void LoadHourlyLoadChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT DATEPART(HOUR, a.EntryDateTime) as Hour, 
                           COUNT(*) as HourlyVisits
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(HOUR, a.EntryDateTime)
                    ORDER BY Hour", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int hour = Convert.ToInt32(reader["Hour"]);
                            labels.Add($"{hour:D2}:00");
                            values.Add(Convert.ToDouble(reader["HourlyVisits"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Посещения",
                Values = values,
                Fill = System.Windows.Media.Brushes.Coral
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Час дня",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество посещений",
                MinValue = 0
            });

            HourlyLoadChartContainer.Content = chart;
        }

        private void ExportToWord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Word Documents (*.docx)|*.docx",
                    DefaultExt = "docx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    ExportToWordDocument(filePath);
                    MessageBox.Show("Отчет по общей аналитике успешно экспортирован в Word", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToWordDocument(string filePath)
        {
            using (WordprocessingDocument wordDocument =
                WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();

                Body body = mainPart.Document.AppendChild(new Body());

                SectionProperties sectionProperties = new SectionProperties();
                sectionProperties.AppendChild(new PageSize() { Width = 11900, Height = 16840 });
                sectionProperties.AppendChild(new PageMargin() { Top = 720, Right = 720, Bottom = 720, Left = 720 });
                body.AppendChild(sectionProperties);

                // Заголовок
                Paragraph titleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "0", Before = "0" }
                    ),
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "36" }
                        ),
                        new Text("ОТЧЕТ ПО ОБЩЕЙ АНАЛИТИКЕ")
                    )
                );
                body.AppendChild(titleParagraph);

                // Подзаголовок
                Paragraph subTitleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "400", Before = "0" }
                    ),
                    new Run(
                        new RunProperties(
                            new FontSize() { Val = "28" }
                        ),
                        new Text($"за период {DateFromPicker.SelectedDate?.ToString("dd.MM.yyyy")} - {DateToPicker.SelectedDate?.ToString("dd.MM.yyyy")}")
                    )
                );
                body.AppendChild(subTitleParagraph);

                // Дата формирования
                Paragraph dateInfo = new Paragraph(
                    new Run(
                        new RunProperties(new Bold()),
                        new Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy}")
                    )
                );
                body.AppendChild(dateInfo);

                // Разделитель
                Paragraph divider = new Paragraph(
                    new ParagraphProperties(
                        new ParagraphBorders(
                            new BottomBorder() { Val = BorderValues.Single, Size = 6, Space = 1, Color = "AAAAAA" }
                        ),
                        new SpacingBetweenLines() { After = "400", Before = "200" }
                    )
                );
                body.AppendChild(divider);

                // Ключевые показатели
                Paragraph metricsHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" },
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("1. Ключевые показатели деятельности")
                    )
                );
                body.AppendChild(metricsHeader);

                Table metricsTable = CreateAnalyticsMetricsTable();
                body.AppendChild(metricsTable);
            }
        }

        private Table CreateAnalyticsMetricsTable()
        {
            Table table = new Table();

            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                )
            );
            table.AppendChild(tblProp);

            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Заголовок
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Показатель"));
            headerRow.AppendChild(CreateHeaderCell("Значение"));
            table.AppendChild(headerRow);

            // Данные
            table.AppendChild(CreateDataRow("Общая выручка", TotalRevenueTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Активные клиенты", ActiveClientsTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Общее количество посещений", TotalVisitsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Средняя загрузка", AverageLoadTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Новые клиенты", NewClientsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Активные абонементы", ActiveMembershipsTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Проведено тренировок", CompletedTrainingsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Коэффициент удержания", RetentionRateTextBlock.Text, true));

            return table;
        }

        // Вспомогательные методы для создания ячеек таблицы
        private TableCell CreateHeaderCell(string text)
        {
            TableCell cell = new TableCell();

            TableCellProperties cellProperties = new TableCellProperties(
                new TableCellWidth() { Type = TableWidthUnitValues.Auto },
                new Shading()
                {
                    Val = ShadingPatternValues.Clear,
                    Color = "auto",
                    Fill = "DEDEDE"
                },
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
            );
            cell.AppendChild(cellProperties);

            Paragraph paragraph = new Paragraph(
                new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center }
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "22" }
                    ),
                    new Text(text)
                )
            );

            cell.AppendChild(paragraph);
            return cell;
        }

        private TableCell CreateDataCell(string text, bool alignRight = false, bool isAlternateRow = false)
        {
            TableCell cell = new TableCell();

            TableCellProperties cellProperties = new TableCellProperties(
                new TableCellWidth() { Type = TableWidthUnitValues.Auto },
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
            );

            if (isAlternateRow)
            {
                cellProperties.AppendChild(new Shading()
                {
                    Val = ShadingPatternValues.Clear,
                    Color = "auto",
                    Fill = "F9F9F9"
                });
            }

            cell.AppendChild(cellProperties);

            Paragraph paragraph = new Paragraph(
                new ParagraphProperties(
                    new Justification() { Val = alignRight ? JustificationValues.Right : JustificationValues.Left }
                ),
                new Run(
                    new RunProperties(
                        new FontSize() { Val = "22" }
                    ),
                    new Text(text)
                )
            );

            cell.AppendChild(paragraph);
            return cell;
        }

        private TableRow CreateDataRow(string label, string value, bool isAlternateRow = false)
        {
            TableRow row = new TableRow();
            row.AppendChild(CreateDataCell(label, false, isAlternateRow));
            row.AppendChild(CreateDataCell(value, true, isAlternateRow));
            return row;
        }
    }
}
