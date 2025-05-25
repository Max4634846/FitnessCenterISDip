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

namespace FitnessCenterIS.View.Pages
{
    public partial class AttendanceReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public AttendanceReportPage()
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
                LoadAttendanceData();
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
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1);

            try
            {
                LoadKeyMetrics();
                LoadDailyVisitsChart();
                LoadPopularRoomsChart();
                LoadHourlyVisitsChart();
                LoadWeeklyVisitsChart();
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

                // Уникальные посетители
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT a.ClientID)
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    UniqueVisitorsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Среднее время посещения (в минутах)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AVG(DATEDIFF(MINUTE, a.EntryDateTime, a.ExitDateTime))
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    AND a.ExitDateTime IS NOT NULL
                    AND a.ExitDateTime > a.EntryDateTime", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        AverageVisitTimeTextBlock.Text = $"{Convert.ToInt32(result)} мин";
                    }
                    else
                    {
                        AverageVisitTimeTextBlock.Text = "Н/Д";
                    }
                }

                // Пиковое время (час с наибольшим количеством посещений)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 DATEPART(HOUR, a.EntryDateTime) as PeakHour
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(HOUR, a.EntryDateTime)
                    ORDER BY COUNT(*) DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        PeakTimeTextBlock.Text = $"{result}:00";
                    }
                    else
                    {
                        PeakTimeTextBlock.Text = "Н/Д";
                    }
                }

                // Самый активный день
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 CONVERT(date, a.EntryDateTime) as ActiveDay
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY CONVERT(date, a.EntryDateTime)
                    ORDER BY COUNT(*) DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    if (result != null && DateTime.TryParse(result.ToString(), out DateTime activeDay))
                    {
                        MostActiveDayTextBlock.Text = activeDay.ToString("dd.MM.yyyy");
                    }
                    else
                    {
                        MostActiveDayTextBlock.Text = "Н/Д";
                    }
                }

                // Средняя посещаемость в день
                int totalDays = (endDate.Date - startDate.Date).Days + 1;
                if (int.TryParse(TotalVisitsTextBlock.Text, out int totalVisits) && totalDays > 0)
                {
                    double avgDailyVisits = (double)totalVisits / totalDays;
                    AverageDailyVisitsTextBlock.Text = $"{avgDailyVisits:F1}";
                }
                else
                {
                    AverageDailyVisitsTextBlock.Text = "0";
                }
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

        private void LoadPopularRoomsChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Исправленный запрос через расписание
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 5 r.Name as RoomName, COUNT(*) as VisitCount
                    FROM Schedules s
                    INNER JOIN Rooms r ON s.RoomID = r.RoomID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    AND s.RoomID IS NOT NULL
                    GROUP BY r.RoomID, r.Name
                    ORDER BY VisitCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool hasData = false;
                        while (reader.Read())
                        {
                            hasData = true;
                            seriesCollection.Add(new PieSeries
                            {
                                Title = reader["RoomName"].ToString(),
                                Values = new ChartValues<double> { Convert.ToDouble(reader["VisitCount"]) },
                                DataLabels = true
                            });
                        }

                        // Если нет данных, добавляем заглушку
                        if (!hasData)
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = "Нет данных",
                                Values = new ChartValues<double> { 1 },
                                DataLabels = true,
                                Fill = System.Windows.Media.Brushes.LightGray
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

            PopularRoomsChartContainer.Content = chart;
        }

        private void LoadHourlyVisitsChart()
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
                Title = "Час дня",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество посещений",
                MinValue = 0
            });

            HourlyVisitsChartContainer.Content = chart;
        }

        private void LoadWeeklyVisitsChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string> { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Инициализируем массив для дней недели (1=Понедельник, 7=Воскресенье)
                double[] weeklyData = new double[7];

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT DATEPART(WEEKDAY, a.EntryDateTime) as WeekDay, 
                           COUNT(*) as WeeklyVisits
                    FROM Attendances a
                    WHERE a.EntryDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY DATEPART(WEEKDAY, a.EntryDateTime)", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int weekDay = Convert.ToInt32(reader["WeekDay"]);
                            double visits = Convert.ToDouble(reader["WeeklyVisits"]);

                            // SQL DATEPART возвращает: 1=Воскресенье, 2=Понедельник, ..., 7=Суббота
                            // Преобразуем в: 0=Понедельник, 1=Вторник, ..., 6=Воскресенье
                            int dayIndex = weekDay == 1 ? 6 : weekDay - 2;
                            weeklyData[dayIndex] = visits;
                        }
                    }
                }

                // Добавляем данные в ChartValues
                for (int i = 0; i < 7; i++)
                {
                    values.Add(weeklyData[i]);
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
                Title = "День недели",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество посещений",
                MinValue = 0
            });

            WeeklyVisitsChartContainer.Content = chart;
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
                    MessageBox.Show("Отчет по посещаемости успешно экспортирован в Word", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        new Text("ОТЧЕТ ПО ПОСЕЩАЕМОСТИ")
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
                        new Text("1. Ключевые показатели посещаемости")
                    )
                );
                body.AppendChild(metricsHeader);

                Table metricsTable = CreateAttendanceMetricsTable();
                body.AppendChild(metricsTable);
            }
        }

        private Table CreateAttendanceMetricsTable()
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
            table.AppendChild(CreateDataRow("Общее количество посещений", TotalVisitsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Уникальные посетители", UniqueVisitorsTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Среднее время посещения", AverageVisitTimeTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Пиковое время", PeakTimeTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Самый активный день", MostActiveDayTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Средняя посещаемость в день", AverageDailyVisitsTextBlock.Text, true));

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
