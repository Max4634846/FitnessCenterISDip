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
    public partial class FinancialReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public FinancialReportPage()
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
                LoadDailyRevenueChart();
                LoadPaymentMethodsChart();
                LoadServiceSalesChart();
                LoadTrainerRevenueChart();
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

                // Количество продаж
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    TotalSalesTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Средний чек
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(AVG(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal avgCheck = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    AverageCheckTextBlock.Text = $"{avgCheck:N0} ₽";
                }

                // Общие платежи
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(p.Amount), 0)
                    FROM Payments p
                    WHERE p.DateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal payments = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    TotalPaymentsTextBlock.Text = $"{payments:N0} ₽";
                }

                // Общие скидки
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(s.DiscountAmount), 0)
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal discounts = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    TotalDiscountsTextBlock.Text = $"{discounts:N0} ₽";
                }

                // Лучший день по выручке
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 CONVERT(date, s.SaleDateTime) as SaleDate
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CONVERT(date, s.SaleDateTime)
                    ORDER BY SUM(s.PriceSold) DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    if (result != null && DateTime.TryParse(result.ToString(), out DateTime bestDay))
                    {
                        BestDayTextBlock.Text = bestDay.ToString("dd.MM.yyyy");
                    }
                    else
                    {
                        BestDayTextBlock.Text = "Н/Д";
                    }
                }
            }
        }

        private void LoadDailyRevenueChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT CONVERT(date, s.SaleDateTime) as SaleDate, 
                           SUM(s.PriceSold) as DailyRevenue
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CONVERT(date, s.SaleDateTime)
                    ORDER BY SaleDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime date = Convert.ToDateTime(reader["SaleDate"]);
                            labels.Add(date.ToString("dd.MM"));
                            values.Add(Convert.ToDouble(reader["DailyRevenue"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new LineSeries
            {
                Title = "Выручка",
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
                Title = "Выручка (₽)",
                MinValue = 0
            });

            DailyRevenueChartContainer.Content = chart;
        }

        private void LoadPaymentMethodsChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT pm.Name as PaymentMethod, SUM(p.Amount) as TotalAmount
                    FROM Payments p
                    JOIN PaymentMethods pm ON p.PaymentMethodID = pm.PaymentMethodID
                    WHERE p.DateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY pm.PaymentMethodID, pm.Name
                    ORDER BY TotalAmount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = reader["PaymentMethod"].ToString(),
                                Values = new ChartValues<double> { Convert.ToDouble(reader["TotalAmount"]) },
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

            PaymentMethodsChartContainer.Content = chart;
        }

        private void LoadServiceSalesChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 se.Name as ServiceName, SUM(s.PriceSold) as ServiceRevenue
                    FROM Sales s
                    JOIN SeasonticketServices ss ON s.SeasonticketServiceID = ss.SeasonticketServiceID
                    JOIN Seasontickets st ON ss.SeasonticketID = st.SeasonticketID
                    JOIN Services se ON ss.ServiceID = se.ServiceID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY se.ServiceID, se.Name
                    ORDER BY ServiceRevenue DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["ServiceName"].ToString());
                            values.Add(Convert.ToDouble(reader["ServiceRevenue"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Выручка",
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
                Title = "Услуга",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Выручка (₽)",
                MinValue = 0
            });

            ServiceSalesChartContainer.Content = chart;
        }

        private void LoadTrainerRevenueChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, 
                           SUM(s.PriceSold) as TrainerRevenue
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    AND s.TrainerID IS NOT NULL
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY TrainerRevenue DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["TrainerName"].ToString());
                            values.Add(Convert.ToDouble(reader["TrainerRevenue"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Выручка",
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
                Title = "Тренер",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Выручка (₽)",
                MinValue = 0
            });

            TrainerRevenueChartContainer.Content = chart;
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
                    MessageBox.Show("Финансовый отчет успешно экспортирован в Word", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        new Text("ФИНАНСОВЫЙ ОТЧЕТ")
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
                        new Text("1. Ключевые финансовые показатели")
                    )
                );
                body.AppendChild(metricsHeader);

                Table metricsTable = CreateFinancialMetricsTable();
                body.AppendChild(metricsTable);

                // Выручка по дням
                Paragraph dailyRevenueHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" },
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("2. Выручка по дням")
                    )
                );
                body.AppendChild(dailyRevenueHeader);

                Table dailyRevenueTable = CreateDailyRevenueTable();
                body.AppendChild(dailyRevenueTable);

                // Способы оплаты
                Paragraph paymentMethodsHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" },
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("3. Анализ способов оплаты")
                    )
                );
                body.AppendChild(paymentMethodsHeader);

                Table paymentMethodsTable = CreatePaymentMethodsTable();
                body.AppendChild(paymentMethodsTable);
            }
        }

        private Table CreateFinancialMetricsTable()
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
            table.AppendChild(CreateDataRow("Количество продаж", TotalSalesTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Средний чек", AverageCheckTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Общие платежи", TotalPaymentsTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Скидки", TotalDiscountsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Лучший день", BestDayTextBlock.Text, true));

            return table;
        }

        private Table CreateDailyRevenueTable()
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
                new GridColumn() { Width = "5000" },
                new GridColumn() { Width = "5000" }
            );
            table.AppendChild(tableGrid);

            // Заголовок
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Дата"));
            headerRow.AppendChild(CreateHeaderCell("Выручка (₽)"));
            table.AppendChild(headerRow);

            // Данные
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT CONVERT(date, s.SaleDateTime) as SaleDate, 
                           SUM(s.PriceSold) as DailyRevenue
                    FROM Sales s
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CONVERT(date, s.SaleDateTime)
                    ORDER BY SaleDate DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        while (reader.Read())
                        {
                            isAlternateRow = !isAlternateRow;
                            DateTime date = Convert.ToDateTime(reader["SaleDate"]);
                            decimal revenue = Convert.ToDecimal(reader["DailyRevenue"]);

                            TableRow dataRow = CreateDataRow(
                                date.ToString("dd.MM.yyyy"),
                                $"{revenue:N0} ₽",
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }
                    }
                }
            }

            return table;
        }

        private Table CreatePaymentMethodsTable()
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
                new GridColumn() { Width = "5000" },
                new GridColumn() { Width = "5000" }
            );
            table.AppendChild(tableGrid);

            // Заголовок
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Способ оплаты"));
            headerRow.AppendChild(CreateHeaderCell("Сумма (₽)"));
            table.AppendChild(headerRow);

            // Данные
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT pm.Name as PaymentMethod, SUM(p.Amount) as TotalAmount
                    FROM Payments p
                    JOIN PaymentMethods pm ON p.PaymentMethodID = pm.PaymentMethodID
                    WHERE p.DateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY pm.PaymentMethodID, pm.Name
                    ORDER BY TotalAmount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        while (reader.Read())
                        {
                            isAlternateRow = !isAlternateRow;
                            decimal amount = Convert.ToDecimal(reader["TotalAmount"]);

                            TableRow dataRow = CreateDataRow(
                                reader["PaymentMethod"].ToString(),
                                $"{amount:N0} ₽",
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }
                    }
                }
            }

            return table;
        }

        // Вспомогательные методы для создания ячеек таблицы (аналогично коду тренеров)
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
