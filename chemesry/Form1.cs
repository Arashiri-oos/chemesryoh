using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace chemesry
{
    public partial class Form1 : Form
    {
        private int _myValue;

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

        // Список всех активных элементов на экране
        private List<VisualElement> activeElements = new List<VisualElement>();

        // Текущий элемент, выбранный для размещения
        private string selectedElementToPlace = null;

        // ИСПРАВЛЕНО: Явное указание таймера
        private System.Windows.Forms.Timer physicsTimer;

        public Form1()
        {
            InitializeComponent();

            // Включаем двойную буферизацию
            this.DoubleBuffered = true;

            MyValue = 0;

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

            // Событие клика по форме для спавна
            this.MouseClick += Form1_MouseClick;

            button11.Click += MyButtonAction_Click;

        }

        // --- МЕТОДЫ, КОТОРЫХ НЕ ХВАТАЛО НА СКРИНШОТЕ ---

        private void ElementButton_Click(object sender, EventArgs e)
        {
            Button clickedButton = sender as Button;
            selectedElementToPlace = clickedButton.Text;
            this.Text = $"Выбран: {selectedElementToPlace}. Кликните по экрану для создания.";
        }

       
        

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            
            if (selectedElementToPlace != null && Element.Database.ContainsKey(selectedElementToPlace))
            {
                
                Element baseEl = Element.Database[selectedElementToPlace];
                activeElements.Add(new VisualElement(baseEl, e.X, e.Y));
                this.Invalidate();

                MyValue++;

            }
        }

        private void MyButtonAction_Click(object sender, EventArgs e)
        {
          
           

            activeElements.Clear();
            MyValue = 0;
            selectedElementToPlace = null; // Сбрасываем выбор элемента
            this.Invalidate();
        }




        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            // 1. Движение всех элементов (теперь с задержкой в 1 секунду)
            foreach (var el in activeElements)
            {
                el.FramesAlive++; // Увеличиваем время жизни элемента (1 тик = 1 кадр)

                // Начинаем двигать атом только если он "прожил" больше 60 кадров (около 1 секунды)
                if (el.FramesAlive > 60)
                {
                    el.X += el.VX;
                    el.Y += el.VY;

                    int bottomLimit = this.ClientSize.Height - panel1.Height;
                    if (el.X - el.Radius < 0) { el.X = el.Radius; el.VX *= -1; }
                    if (el.X + el.Radius > this.ClientSize.Width) { el.X = this.ClientSize.Width - el.Radius; el.VX *= -1; }
                    if (el.Y - el.Radius < 0) { el.Y = el.Radius; el.VY *= -1; }
                    if (el.Y + el.Radius > bottomLimit) { el.Y = bottomLimit - el.Radius; el.VY *= -1; }
                }
            }

            // 2. Взаимодействие (Авто-Связи и Столкновения)
            for (int i = 0; i < activeElements.Count; i++)
            {
                for (int j = i + 1; j < activeElements.Count; j++)
                {
                    VisualElement a = activeElements[i];
                    VisualElement b = activeElements[j];

                    float dx = b.X - a.X;
                    float dy = b.Y - a.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (distance == 0) { distance = 0.1f; dx = 0.1f; } // Защита от деления на ноль

                    float minDistance = a.Radius + b.Radius;
                    float bondLength = minDistance + 15f;

                    bool areBonded = a.Bonds.Contains(b);

                    if (!areBonded && distance < minDistance)
                    {
                        // ИСПРАВЛЕНИЕ: Берем максимальную валентность из списка характеристик
                        int maxBondsA = 0;
                        foreach (int v in a.BaseElement.Valence) { if (v > maxBondsA) maxBondsA = v; }

                        int maxBondsB = 0;
                        foreach (int v in b.BaseElement.Valence) { if (v > maxBondsB) maxBondsB = v; }

                        // Если у обоих есть свободные руки -> Создаем связь
                        if (a.Bonds.Count < maxBondsA && b.Bonds.Count < maxBondsB)
                        {
                            a.Bonds.Add(b);
                            b.Bonds.Add(a);
                            areBonded = true;
                        }
                        else
                        {
                            // ИСПРАВЛЕНИЕ: Отскок срабатывает ТОЛЬКО если оба элемента уже вышли из заморозки
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

                    // 3. Алгоритм жёсткой палочки (стягивает связанные атомы)
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
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (var el in activeElements)
            {
                el.Draw(e.Graphics);
            }
        }

       
    }
}