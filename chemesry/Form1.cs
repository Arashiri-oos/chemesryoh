using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace chemesry
{
    public partial class Form1 : Form
    {
        private bool isDragging = false;
        private VisualElemen selectedElement = null; // Замените Element на имя вашего класса атома/круга
        private PointF dragOffset;

        private SpatialGrid spatialGrid = new SpatialGrid(100f);
        private List<VisualElement> nearbyBuffer = new List<VisualElement>(); // Буфер для оптимизации GC

        private bool isDeleteMode = false;

        private int _myValue;
        private string _myValuespeed;
        private int speedMultiplier = 0;

        // Настройки камеры
        private float cameraOffsetX = 0f;
        private float cameraOffsetY = 0f;
        private float zoom = 1.0f;

        // Перетаскивание
        private bool isPanning = false;
        private Point lastMousePos;
        private VisualElement draggedElement = null;

        // Границы мира
        private int arenaWidth = 3000;
        private int arenaHeight = 3000;

        private Random rand = new Random();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string MyValuespeed
        {
            get => _myValuespeed;
            set
            {
                _myValuespeed = value;
                label2.Text = _myValuespeed;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int MyValue
        {
            get => _myValue;
            set
            {
                _myValue = value;
                label1.Text = _myValue.ToString();
            }
        }

        private List<VisualElement> activeElements = new List<VisualElement>();
        private string selectedElementToPlace = null;
        private System.Windows.Forms.Timer physicsTimer;

        public Form1()
        {
            InitializeComponent();

            //this.FormBorderStyle = FormBorderStyle.FixedSingle;
            // this.MaximizeBox = false;
            //  this.MinimizeBox = false;
            this.DoubleBuffered = true;

            MyValue = 0;
            MyValuespeed = "stop";

            physicsTimer = new System.Windows.Forms.Timer();
            physicsTimer.Interval = 16;
            physicsTimer.Tick += PhysicsTimer_Tick;
            physicsTimer.Start();

            // Привязываем ТОЛЬКО кнопки элементов из panel1
            foreach (Control control in panel1.Controls)
            {
                if (control is Button btn && btn != buttonDelete && btn != buttonstopspeed &&
                    btn != buttondefspeed && btn != button2xspeed && btn != button11)
                {
                    btn.Click += ElementButton_Click;
                }
            }

            // Управляющие кнопки
            buttonstopspeed.Click += stopspeed;
            buttondefspeed.Click += defspeed;
            button2xspeed.Click += x_speed;
            buttonDelete.Click += buttonDelete_Click;
            button11.Click += MyButtonAction_Click;

            // События мыши
            this.MouseWheel += Form1_MouseWheel;
            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;
        }

        private PointF ScreenToWorld(int screenX, int screenY)
        {
            float worldX = (screenX - cameraOffsetX) / zoom;
            float worldY = (screenY - cameraOffsetY) / zoom;
            return new PointF(worldX, worldY);
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            isDeleteMode = true;
            selectedElementToPlace = null;
            this.Text = "Режим удаления: кликните по атому, чтобы стереть его.";
        }

        private void ElementButton_Click(object sender, EventArgs e)
        {
            if (sender is Button clickedButton)
            {
                selectedElementToPlace = clickedButton.Text;
                isDeleteMode = false;
                this.Text = $"Выбран: {selectedElementToPlace}. Кликните по экрану для создания.";
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                isPanning = true;
                lastMousePos = e.Location;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                PointF worldPos = ScreenToWorld(e.X, e.Y);

                VisualElement clickedElement = null;
                foreach (var el in activeElements)
                {
                    float dx = worldPos.X - el.X;
                    float dy = worldPos.Y - el.Y;
                    if (dx * dx + dy * dy <= el.Radius * el.Radius)
                    {
                        clickedElement = el;
                        break;
                    }
                }

                // --- РЕЖИМ УДАЛЕНИЯ ---
                if (isDeleteMode && clickedElement != null)
                {
                    foreach (var el in activeElements)
                    {
                        el.Bonds.Remove(clickedElement);
                    }

                    clickedElement.Bonds.Clear();
                    activeElements.Remove(clickedElement);

                    if (draggedElement == clickedElement)
                        draggedElement = null;

                    MyValue = activeElements.Count;
                    this.Invalidate();
                    return;
                }

                // Захват атома
                if (clickedElement != null)
                {
                    draggedElement = clickedElement;
                    draggedElement.VX = 0;
                    draggedElement.VY = 0;
                    return;
                }

                // Спавн атома
                if (selectedElementToPlace != null && VisualElemen.Database.ContainsKey(selectedElementToPlace))
                {
                    VisualElemen baseEl = VisualElemen.Database[selectedElementToPlace];
                    activeElements.Add(new VisualElement(baseEl, worldPos.X, worldPos.Y));
                    MyValue = activeElements.Count;
                    this.Invalidate();
                }
            }
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                cameraOffsetX += (e.X - lastMousePos.X);
                cameraOffsetY += (e.Y - lastMousePos.Y);
                lastMousePos = e.Location;
                this.Invalidate();
            }
            else if (draggedElement != null)
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
            if (e.Delta > 0) zoom += 0.1f;
            else zoom -= 0.1f;

            if (zoom < 0.2f) zoom = 0.2f;
            if (zoom > 5.0f) zoom = 5.0f;

            this.Invalidate();
        }

        private void MyButtonAction_Click(object sender, EventArgs e)
        {
            activeElements.Clear();
            MyValue = 0;
            selectedElementToPlace = null;
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
            int steps = speedMultiplier == 0 ? 1 : speedMultiplier;

            for (int step = 0; step < steps; step++)
            {
                // 1. Движение атомов
                if (speedMultiplier > 0)
                {
                    foreach (var el in activeElements)
                    {

                        if (isDragging && el.Equals(selectedElement))
                            continue;
                        el.FramesAlive++;

                        if (el.FramesAlive > 60)
                        {
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

                            if (el.X - el.Radius < 0) { el.X = el.Radius; el.VX *= -1; }
                            if (el.X + el.Radius > arenaWidth) { el.X = arenaWidth - el.Radius; el.VX *= -1; }
                            if (el.Y - el.Radius < 0) { el.Y = el.Radius; el.VY *= -1; }
                            if (el.Y + el.Radius > arenaHeight) { el.Y = arenaHeight - el.Radius; el.VY *= -1; }
                        }
                    }
                }

                // 2. Стягивание существующих связей
                foreach (var a in activeElements)
                {
                    foreach (var b in a.Bonds)
                    {
                        if (activeElements.IndexOf(a) >= activeElements.IndexOf(b)) continue;

                        float dx = b.X - a.X;
                        float dy = b.Y - a.Y;
                        float distSq = dx * dx + dy * dy;

                        float minDistance = a.Radius + b.Radius;
                        float bondLength = minDistance + 45f;
                        float targetDistSq = bondLength * bondLength;

                        if (Math.Abs(distSq - targetDistSq) < 0.01f) continue;

                        float distance = (float)Math.Sqrt(distSq);
                        if (distance == 0) { distance = 0.1f; dx = 0.1f; }

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

                // 3. Сетка и коллизии
                spatialGrid.Clear();
                foreach (var el in activeElements)
                {
                    spatialGrid.Add(el);
                }

                for (int i = 0; i < activeElements.Count; i++)
                {
                    VisualElement a = activeElements[i];
                    spatialGrid.GetNearby(a, nearbyBuffer); // Используем буфер без лишней памяти

                    foreach (var b in nearbyBuffer)
                    {
                        if (a == b || activeElements.IndexOf(b) <= i || a.Bonds.Contains(b)) continue;

                        float dx = b.X - a.X;
                        float dy = b.Y - a.Y;
                        float distSq = dx * dx + dy * dy;

                        float minDistance = a.Radius + b.Radius;

                        if (distSq > minDistance * minDistance) continue;

                        float distance = (float)Math.Sqrt(distSq);
                        if (distance == 0) { distance = 0.1f; dx = 0.1f; }

                        int maxBondsA = 0;
                        foreach (int v in a.BaseElement.Valence) { if (v > maxBondsA) maxBondsA = v; }

                        int maxBondsB = 0;
                        foreach (int v in b.BaseElement.Valence) { if (v > maxBondsB) maxBondsB = v; }

                        if (a.Bonds.Count < maxBondsA && b.Bonds.Count < maxBondsB)
                        {
                            a.Bonds.Add(b);
                            b.Bonds.Add(a);
                        }
                        else if (a.FramesAlive > 60 && b.FramesAlive > 60)
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
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            e.Graphics.TranslateTransform(cameraOffsetX, cameraOffsetY);
            e.Graphics.ScaleTransform(zoom, zoom);

            // Арена
            using (Pen borderPen = new Pen(Color.Gray, 3))
            {
                borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawRectangle(borderPen, 0, 0, arenaWidth, arenaHeight);
            }

            // Viewport Culling
            float viewLeft = -cameraOffsetX / zoom;
            float viewTop = -cameraOffsetY / zoom;
            float viewRight = viewLeft + (this.ClientSize.Width / zoom);
            float viewBottom = viewTop + (this.ClientSize.Height / zoom);

            // ИСПРАВЛЕНИЕ: Отрисовка линий химических связей
            using (Pen bondPen = new Pen(Color.DarkGray, 4f))
            {
                foreach (var el in activeElements)
                {
                    foreach (var bonded in el.Bonds)
                    {
                        // Рисуем одну связь только один раз
                        if (activeElements.IndexOf(el) < activeElements.IndexOf(bonded))
                        {
                            e.Graphics.DrawLine(bondPen, el.X, el.Y, bonded.X, bonded.Y);
                        }
                    }
                }
            }

            // Отрисовка самих атомов
            foreach (var el in activeElements)
            {
                if (el.X + el.Radius >= viewLeft && el.X - el.Radius <= viewRight &&
                    el.Y + el.Radius >= viewTop && el.Y - el.Radius <= viewBottom)
                {
                    el.Draw(e.Graphics);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }

    public class SpatialGrid
    {
        private float cellSize;
        private Dictionary<(int, int), List<VisualElement>> grid = new Dictionary<(int, int), List<VisualElement>>();

        public SpatialGrid(float cellSize)
        {
            this.cellSize = cellSize;
        }

        public void Clear() => grid.Clear();

        public void Add(VisualElement el)
        {
            var cell = GetCell(el.X, el.Y);
            if (!grid.TryGetValue(cell, out var list))
            {
                list = new List<VisualElement>();
                grid[cell] = list;
            }
            list.Add(el);
        }

        // Оптимизированный метод без создания лишних new List()
        public void GetNearby(VisualElement el, List<VisualElement> resultBuffer)
        {
            resultBuffer.Clear();
            var (cx, cy) = GetCell(el.X, el.Y);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (grid.TryGetValue((cx + x, cy + y), out var list))
                    {
                        resultBuffer.AddRange(list);
                    }
                }
            }
        }

        private (int, int) GetCell(float x, float y) => ((int)(x / cellSize), (int)(y / cellSize));
    }
}