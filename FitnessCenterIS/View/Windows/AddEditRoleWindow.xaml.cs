using System;
using System.Linq;
using System.Windows;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddEditRoleWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private bool _isEditMode = false;
        private int _roleId = 0;

        // Защищенные названия ролей, которые нельзя использовать при создании новых ролей
        private readonly string[] _protectedRoleNames = new[]
        {
            "Администратор стойки",
            "Системный администратор"
        };

        public AddEditRoleWindow()
        {
            InitializeComponent();
            _isEditMode = false;
            WindowTitle.Text = "Новая роль";
        }

        public AddEditRoleWindow(int roleId)
        {
            InitializeComponent();
            _isEditMode = true;
            _roleId = roleId;
            WindowTitle.Text = "Редактирование роли";

            LoadRoleData(roleId);
        }

        private void LoadRoleData(int roleId)
        {
            try
            {
                var role = _dbContext.Roles.FirstOrDefault(r => r.RoleID == roleId);
                if (role != null)
                {
                    RoleNameTextBox.Text = role.Name;
                    RoleDescriptionTextBox.Text = role.Description;
                    RoleIdTextBox.Text = role.RoleID.ToString();
                }
                else
                {
                    MessageBox.Show("Роль не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string roleName = RoleNameTextBox.Text.Trim();
            string roleDescription = RoleDescriptionTextBox.Text.Trim();

            // Валидация ввода
            if (string.IsNullOrWhiteSpace(roleName))
            {
                MessageBox.Show("Пожалуйста, введите название роли", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                RoleNameTextBox.Focus();
                return;
            }

            // Проверка на защищенные имена ролей
            if (!_isEditMode && _protectedRoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Нельзя создать роль с названием '{roleName}'. Это зарезервированное системное название.",
                    "Ограничение системы", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка на уникальность имени роли
            bool nameExists = _dbContext.Roles.Any(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)
                                                   && (!_isEditMode || r.RoleID != _roleId));
            if (nameExists)
            {
                MessageBox.Show($"Роль с названием '{roleName}' уже существует. Пожалуйста, выберите другое название.",
                    "Дублирование названия", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_isEditMode)
                {
                    // Режим редактирования
                    var roleToUpdate = _dbContext.Roles.Find(_roleId);
                    if (roleToUpdate != null)
                    {
                        roleToUpdate.Name = roleName;
                        roleToUpdate.Description = roleDescription;
                        _dbContext.SaveChanges();
                        MessageBox.Show("Роль успешно обновлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.DialogResult = true;
                    }
                }
                else
                {
                    // Режим добавления
                    var newRole = new Roles
                    {
                        Name = roleName,
                        Description = roleDescription
                    };
                    _dbContext.Roles.Add(newRole);
                    _dbContext.SaveChanges();
                    MessageBox.Show("Роль успешно добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}