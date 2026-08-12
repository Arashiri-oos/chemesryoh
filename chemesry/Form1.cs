using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;


namespace chemesry
{
    public partial class Form1 : Form
    {
        int b = 1;
        private int _myValue;
        private string _myValuespeed;
        private int speedMultiplier = 0;
        // Настройки камеры
        private float cameraOffsetX = 0f; // Смещение камеры по X
        private float cameraOffsetY = 0f; // Смещение камеры по Y
        private float zoom = 1.0f;        // Масштаб (1.0 = 100%)

        // Переменные для перетаскивания карты
        private bool isPanning = false;
        private Point lastMousePos;
        // Атом, который мы сейчас тащим мышкой (если ничего не тащим, тут null)
        private VisualElement draggedElement = null;
        // Границы нашего огромного мира
        private int arenaWidth = 3000;
        private int arenaHeight = 3000;

        // Генератор случайных чисел для броуновского (теплового) движения
        private Random rand = new Random();
        // Создаем свойство со скрытием от дизайнера, чтобы не было ошибки WFO1000
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string MyValuespeed
        {
            get { return _myValuespeed; }
            set
            {
                _myValuespeed = value;
                // Обновляем текст на экране при каждом изменении переменной
                label2.Text = _myValuespeed;
            }
            
        }
        

        // Создаем свойство со скрытием от дизайнера, чтобы не было ошибки WFO1000
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int MyValue
        {
            get { return _myValue; }
            set
            {
                _myValue = value;
                // Обновляем текст на экране при каждом изменении переменной
                label1.Text = _myValue.ToString();
            }
        }


        private PointF ScreenToWorld(int screenX, int screenY)
        {
            // Математика перевода координат мыши в координаты физического мира с учетом зума и сдвига
            float worldX = (screenX - cameraOffsetX) / zoom;
            float worldY = (screenY - cameraOffsetY) / zoom;
            return new PointF(worldX, worldY);
        }


        // Список всех активных элементов на экране
        private List<VisualElement> activeElements = new List<VisualElement>();
        
        // Текущий элемент, выбранный для размещения
        private string selectedElementToPlace = null;

        // ИСПРАВЛЕНО: Явное указание таймера
        private System.Windows.Forms.Timer physicsTimer;

        public Form1()
        {
            InitializeComponent();

            // 1. Запрещаем менять размер мышкой (рамка станет фиксированной)
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            // 2. Отключаем кнопку "Развернуть" (Maximize)
            this.MaximizeBox = false;

            // 3. Отключаем кнопку "Свернуть" (Minimize)
            this.MinimizeBox = false;

            // Включаем двойную буферизацию
            this.DoubleBuffered = true;

            MyValue = 0;

            MyValuespeed = "stop";

            // ИСПРАВЛЕНО: Явное указание при создании объекта таймера
            physicsTimer = new System.Windows.Forms.Timer();
            physicsTimer.Interval = 16;
            physicsTimer.Tick += PhysicsTimer_Tick; // Привязка заработает, так как метод теперь есть ниже
            physicsTimer.Start();

            // Привязываем кнопки к одному обработчику
            foreach (Control control in panel1.Controls)
            {
                if (control is Button btn)
                {
                    btn.Click += ElementButton_Click;
                }
            }

            buttonstopspeed.Click += stopspeed;
            buttondefspeed.Click += defspeed;
            button2xspeed.Click += x_speed;
            this.MouseWheel += Form1_MouseWheel;

            // Событие клика по форме для спавна
            // Подписываемся на новые события мыши (кликать, тащить, отпускать)
            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;

            button11.Click += MyButtonAction_Click;


        }

        // --- МЕТОДЫ, КОТОРЫХ НЕ ХВАТАЛО НА СКРИНШОТЕ ---

        private void ElementButton_Click(object sender, EventArgs e)
        {
            Button clickedButton = sender as Button;
            selectedElementToPlace = clickedButton.Text;
            this.Text = $"Выбран: {selectedElementToPlace}. Кликните по экрану для создания.";
        }




        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            // ПРАВАЯ КНОПКА: Начинаем двигать камеру
            if (e.Button == MouseButtons.Right)
            {
                isPanning = true;
                lastMousePos = e.Location;
                return;
            }

            // ЛЕВАЯ КНОПКА: Работаем с атомами
            if (e.Button == MouseButtons.Left)
            {
                // Переводим пиксель клика в реальную координату мира
                PointF worldPos = ScreenToWorld(e.X, e.Y);

                foreach (var el in activeElements)
                {
                    float dx = worldPos.X - el.X;
                    float dy = worldPos.Y - el.Y;
                    if (dx * dx + dy * dy <= el.Radius * el.Radius)
                    {
                        draggedElement = el;
                        draggedElement.VX = 0;
                        draggedElement.VY = 0;
                        return;
                    }
                }

                // Спавн нового атома (по координатам мира, а не экрана)
                if (selectedElementToPlace != null && Element.Database.ContainsKey(selectedElementToPlace))
                {
                    Element baseEl = Element.Database[selectedElementToPlace];
                    activeElements.Add(new VisualElement(baseEl, worldPos.X, worldPos.Y));
                    MyValue = activeElements.Count;
                    this.Invalidate();
                }
            }
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning) // Если тащим карту правой кнопкой
            {
                cameraOffsetX += (e.X - lastMousePos.X);
                cameraOffsetY += (e.Y - lastMousePos.Y);
                lastMousePos = e.Location;
                this.Invalidate();
            }
            else if (draggedElement != null) // Если тащим атом левой кнопкой
            {
                PointF worldPos = ScreenToWorld(e.X, e.Y);
                draggedElement.X = worldPos.X;
                draggedElement.Y = worldPos.Y;
                this.Invalidate();
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) isPanning = false;
            if (e.Button == MouseButtons.Left) draggedElement = null;
        }

        private void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            // Меняем масштаб на 10% в зависимости от прокрутки
            if (e.Delta > 0) zoom += 0.1f;
            else zoom -= 0.1f;

            // Ограничиваем, чтобы нельзя было уйти в минус или зумировать до безумия
            if (zoom < 0.2f) zoom = 0.2f;
            if (zoom > 5.0f) zoom = 5.0f;

            this.Invalidate();
        }
        private void MyButtonAction_Click(object sender, EventArgs e)
        {



            activeElements.Clear();
            MyValue = 0;
            selectedElementToPlace = null; // Сбрасываем выбор элемента
            this.Invalidate();
        }

        private void stopspeed(object sender, EventArgs e)
        {
            speedMultiplier = 0;
            MyValuespeed = "stop";
        }

        private void defspeed(object sender, EventArgs e)
        {
            speedMultiplier = 1;
            MyValuespeed = "1x";
        }

        private void x_speed(object sender, EventArgs e)
        {
            speedMultiplier = 2;
            MyValuespeed = "2x";
        }


        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            // Если скорость 0, делаем 1 итерацию для расчета связей, но без полета и дрожания
            int steps = speedMultiplier == 0 ? 1 : speedMultiplier;

            for (int step = 0; step < steps; step++)
            {
                // 1. Движение элементов (работает ТОЛЬКО если скорость не 0)
                if (speedMultiplier > 0)
                {
                    foreach (var el in activeElements)
                    {
                        el.FramesAlive++;

                        if (el.FramesAlive > 60) // Если элемент уже "проснулся"
                        {
                            // Тепловое движение
                            float jitterX = (float)(rand.NextDouble() - 0.5) * 1.0f;
                            float jitterY = (float)(rand.NextDouble() - 0.5) * 1.0f;

                            el.VX += jitterX;
                            el.VY += jitterY;

                            float maxSpeed = 10f;
                            if (el.VX > maxSpeed) el.VX = maxSpeed;
                            if (el.VX < -maxSpeed) el.VX = -maxSpeed;
                            if (el.VY > maxSpeed) el.VY = maxSpeed;
                            if (el.VY < -maxSpeed) el.VY = -maxSpeed;

                            el.X += el.VX;
                            el.Y += el.VY;

                            // Границы арены
                            if (el.X - el.Radius < 0) { el.X = el.Radius; el.VX *= -1; }
                            if (el.X + el.Radius > arenaWidth) { el.X = arenaWidth - el.Radius; el.VX *= -1; }
                            if (el.Y - el.Radius < 0) { el.Y = el.Radius; el.VY *= -1; }
                            if (el.Y + el.Radius > arenaHeight) { el.Y = arenaHeight - el.Radius; el.VY *= -1; }
                        }
                    }
                }

                // 2. и 3. Взаимодействие, столкновения и жесткие палочки (работают ВСЕГДА, даже на паузе!)
                for (int i = 0; i < activeElements.Count; i++)
                {
                    for (int j = i + 1; j < activeElements.Count; j++)
                    {
                        VisualElement a = activeElements[i];
                        VisualElement b = activeElements[j];

                        float dx = b.X - a.X;
                        float dy = b.Y - a.Y;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (distance == 0) { distance = 0.1f; dx = 0.1f; }

                        float minDistance = a.Radius + b.Radius;
                        float bondLength = minDistance + 45f;

                        bool areBonded = a.Bonds.Contains(b);

                        if (!areBonded && distance < minDistance)
                        {
                            int maxBondsA = 0;
                            foreach (int v in a.BaseElement.Valence) { if (v > maxBondsA) maxBondsA = v; }

                            int maxBondsB = 0;
                            foreach (int v in b.BaseElement.Valence) { if (v > maxBondsB) maxBondsB = v; }

                            if (a.Bonds.Count < maxBondsA && b.Bonds.Count < maxBondsB)
                            {
                                a.Bonds.Add(b);
                                b.Bonds.Add(a);
                                areBonded = true;
                            }
                            else
                            {
                                if (a.FramesAlive > 60 && b.FramesAlive > 60)
                                {
                                    float tempVx = a.VX; a.VX = b.VX; b.VX = tempVx;
                                    float tempVy = a.VY; a.VY = b.VY; b.VY = tempVy;

                                    float overlap = 0.5f * (minDistance - distance);
                                    a.X -= (dx / distance) * overlap;
                                    a.Y -= (dy / distance) * overlap;
                                    b.X += (dx / distance) * overlap;
                                    b.Y += (dy / distance) * overlap;
                                }
                            }
                        }

                        // Алгоритм жёсткой палочки (работает при перетаскивании на паузе)
                        if (areBonded)
                        {
                            float avgVx = (a.VX + b.VX) / 2.05f;
                            float avgVy = (a.VY + b.VY) / 2.05f;
                            a.VX = avgVx; b.VX = avgVx;
                            a.VY = avgVy; b.VY = avgVy;

                            float diff = bondLength - distance;
                            float percent = (diff / distance) / 2f;

                            float offsetX = dx * percent;
                            float offsetY = dy * percent;

                            a.X -= offsetX;
                            a.Y -= offsetY;
                            b.X += offsetX;
                            b.Y += offsetY;
                        }
                    }
                }
            }

            // Перерисовываем экран
            this.Invalidate();
        }


      
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Сначала применяем смещение камеры, затем масштаб
            e.Graphics.TranslateTransform(cameraOffsetX, cameraOffsetY);
            e.Graphics.ScaleTransform(zoom, zoom);

            // --- НОВОЕ: Рисуем границы арены пунктирной линией ---
            using (Pen borderPen = new Pen(Color.Gray, 3))
            {
                borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawRectangle(borderPen, 0, 0, arenaWidth, arenaHeight);
            }

            // Рисуем все элементы (Они теперь рисуются в координатах мира)
            foreach (var el in activeElements)
            {
                el.Draw(e.Graphics);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
         