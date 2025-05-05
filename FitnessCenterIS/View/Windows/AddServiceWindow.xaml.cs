using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddServiceWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private Services _service;
        private bool _isEditMode;
        private int? _selectedTrainerId;

        public AddServiceWindow(BDFitnessClubDipEntities dbContext, Services service = null)
        {
            InitializeComponent();
            _dbContext = dbContext;

            if (service != null)
            {
                _service = service;
                _isEditMode = true;
                WindowTitle.Text = "Редактирование услуги";

                // Загружаем связанного тренера, если он есть
                var serviceTrainer = _dbContext.ServiceTrainer
                    .FirstOrDefault(st => st.ServiceID == service.ServiceID);

                if (serviceTrainer != null)
                {
                    _selectedTrainerId = serviceTrainer.TrainerID;
                }
            }
            else
            {
                _service = new Services
                {
                    StatusService = "Активен",
                };
                _isEditMode = false;
                WindowTitle.Text = "Новая услуга";
            }

            DataContext = _service;
            LoadComboBoxes();
        }

        private void LoadComboBoxes()
        {
            ServiceTypeComboBox.ItemsSource = _dbContext.ServiceTypes.ToList();
            ServiceClassificationComboBox.ItemsSource = _dbContext.ServiceClassifications.ToList();

            // Загрузка тренеров
            var trainers = _dbContext.Staffs
                .Include(s => s.Persons)
                .Where(s => s.RoleID == 3)
                .Select(s => new TrainerViewModel
                {
                    TrainerID = s.StaffID,
                    Name = s.Persons.Surname + " " + s.Persons.Name
                })
                .ToList();

            TrainerComboBox.ItemsSource = trainers;

            // Если редактируем услугу и есть назначенный тренер
            if (_isEditMode && _selectedTrainerId.HasValue)
            {
                TrainerComboBox.SelectedValue = _selectedTrainerId.Value;
            }

            // Очищаем существующие элементы в StatusComboBox
            StatusComboBox.Items.Clear();

            // Добавляем русские значения
            StatusComboBox.Items.Add("Активен");
            StatusComboBox.Items.Add("Не активен");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateService())
            {
                try
                {
                    using (var transaction = _dbContext.Database.BeginTransaction())
                    {
                        try
                        {
                            if (_isEditMode)
                            {
                                _dbContext.Entry(_service).State = EntityState.Modified;
                            }
                            else
                            {
                                _dbContext.Services.Add(_service);
                            }

                            _dbContext.SaveChanges();

                            // Обработка назначения тренера
                            // Сначала удаляем все существующие связи с тренерами
                            var existingTrainers = _dbContext.ServiceTrainer
                                .Where(st => st.ServiceID == _service.ServiceID)
                                .ToList();

                            foreach (var trainer in existingTrainers)
                            {
                                _dbContext.ServiceTrainer.Remove(trainer);
                            }

                            // Добавляем нового тренера если он выбран
                            if (TrainerComboBox.SelectedValue != null)
                            {
                                var trainerAssignment = new ServiceTrainer
                                {
                                    ServiceID = _service.ServiceID,
                                    TrainerID = (int)TrainerComboBox.SelectedValue
                                };

                                _dbContext.ServiceTrainer.Add(trainerAssignment);
                            }

                            _dbContext.SaveChanges();
                            transaction.Commit();

                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Ошибка при сохранении в транзакции: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении услуги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateService()
        {
            if (string.IsNullOrWhiteSpace(_service.Name))
            {
                MessageBox.Show("Введите название услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.Price <= 0)
            {
                MessageBox.Show("Цена должна быть больше нуля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.SeviceTypeID == 0)
            {
                MessageBox.Show("Выберите тип услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_service.ServiceClassificationID == 0)
            {
                MessageBox.Show("Выберите классификацию услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void AddServiceClassification_Click(object sender, RoutedEventArgs e)
        {
            var addClassificationWindow = new AddServiceClassificationWindow(_dbContext);
            if (addClassificationWindow.ShowDialog() == true)
            {
                LoadComboBoxes();
                ServiceClassificationComboBox.SelectedItem = addClassificationWindow.ServiceClassificationsDataGrid;
            }
        }

        private void AddServiceType_Click(object sender, RoutedEventArgs e)
        {
            var addTypeWindow = new AddServiceTypeWindow(_dbContext);
            if (addTypeWindow.ShowDialog() == true)
            {
                LoadComboBoxes();
                ServiceTypeComboBox.SelectedItem = addTypeWindow.NewType;
            }
        }
    }

    public class TrainerViewModel
    {
        public int TrainerID { get; set; }
        public string Name { get; set; }
    }
}