using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;

namespace chemesry
{
    public partial class Form1 : Form
    {
        // Переменные для перетаскивания
        private bool isDragging = false;
        private List<VisualElement> draggedMolecule = new List<VisualElement>();
        private VisualElement draggedAtom = null;
        private PointF lastMousePos;

        // Переменные для двойного клика
        private System.Windows.Forms.Timer doubleClickTimer;
        private bool isDoubleClick = false;

        private SpatialGrid spatialGrid = new SpatialGrid(100f);
        private List<VisualElement> nearbyBuffer = new List<VisualElement>();

        private bool isDeleteMode = false;

        private int _myValue;
        private string _myValuespeed;
        private int speedMultiplier = 0;

        // Настройки камеры
        private float cameraOffsetX = 0f;
        private float cameraOffsetY = 0f;
        private float zoom = 1.0f;

        // Перетаскивание камеры (панорамирование)
        private bool isPanning = false;

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

            this.DoubleBuffered = true;

            // Разрешаем Drag & Drop файлов в окно
            this.AllowDrop = true;
            this.DragEnter += Form1_DragEnter;
            this.DragDrop += Form1_DragDrop;

            // Настройка таймера для двойного клика (берем системное время Windows, обычно ~500мс)
            doubleClickTimer = new System.Windows.Forms.Timer();
            doubleClickTimer.Interval = SystemInformation.DoubleClickTime;
            doubleClickTimer.Tick += DoubleClickTimer_Tick;

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

        private void DoubleClickTimer_Tick(object sender, EventArgs e)
        {
            doubleClickTimer.Stop();
            isDoubleClick = false; // Время на двойной клик истекло
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
            this.Text = "Удаление: [ЛКМ] - один атом, [Ctrl+ЛКМ] - вся молекула.";
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

        // --- АЛГОРИТМ ПОИСКА ВСЕЙ МОЛЕКУЛЫ ПО СВЯЗЯМ ---
        private List<VisualElement> GetFullMolecule(VisualElement startAtom)
        {
            List<VisualElement> molecule = new List<VisualElement>();
            Queue<VisualElement> queue = new Queue<VisualElement>();
            HashSet<VisualElement> visited = new HashSet<VisualElement>();

            queue.Enqueue(startAtom);
            visited.Add(startAtom);

            while (queue.Count > 0)
            {
                VisualElement current = queue.Dequeue();
                molecule.Add(current);

                // Перебираем связи текущего атома
                foreach (VisualElement neighbor in current.Bonds)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return molecule;
        }

        // --- СОХРАНЕНИЕ В ФАЙЛ (С УЧЕТОМ СВЯЗЕЙ) ---
        private void SaveMoleculeToFile(List<VisualElement> moleculeToSave)
        {
            if (moleculeToSave.Count == 0) return;

            float centerX = 0, centerY = 0;
            foreach (var el in moleculeToSave)
            {
                centerX += el.X;
                centerY += el.Y;
            }
            centerX /= moleculeToSave.Count;
            centerY /= moleculeToSave.Count;

            MoleculeSaveData data = new MoleculeSaveData();

            // 1. Сохраняем сами атомы (их порядок в списке будет их ID)
            foreach (var el in moleculeToSave)
            {
                data.Atoms.Add(new AtomSaveData
                {
                    ElementType = el.BaseElement.Name, // Убедитесь, что свойство называется Name
                    OffsetX = el.X - centerX,
                    OffsetY = el.Y - centerY
                });
            }

            // 2. Сохраняем связи
            for (int i = 0; i < moleculeToSave.Count; i++)
            {
                VisualElement atomA = moleculeToSave[i];

                foreach (VisualElement atomB in atomA.Bonds)
                {
                    int j = moleculeToSave.IndexOf(atomB);

                    // Сохраняем связь только один раз
                    if (j > i)
                    {
                        data.Bonds.Add(new BondSaveData
                        {
                            AtomIndex1 = i,
                            AtomIndex2 = j
                        });
                    }
                }
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Molecule files (*.mol)|*.mol";
            saveFileDialog.Title = "Сохранить молекулу";

            // Ставим физику на паузу, пока открыто диалоговое окно
            int currentSpeed = speedMultiplier;
            speedMultiplier = 0;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string jsonText = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(saveFileDialog.FileName, jsonText);
                MessageBox.Show("Молекула успешно сохранена!");
            }

            // Возвращаем физику
            speedMultiplier = currentSpeed;
        }

        // --- СОБЫТИЯ DRAG & DROP ДЛЯ ЗАГРУЗКИ ИЗ ФАЙЛА ---
        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string file in files)
            {
                if (Path.GetExtension(file).ToLower() == ".mol")
                {
                    Point clientPoint = this.PointToClient(new Point(e.X, e.Y));
                    PointF worldPos = ScreenToWorld(clientPoint.X, clientPoint.Y);
                    LoadMoleculeFromFile(file, worldPos.X, worldPos.Y);
                }
            }
        }

        private void LoadMoleculeFromFile(string filePath, float worldX, float worldY)
        {
            try
            {
                string jsonText = File.ReadAllText(filePath);
                MoleculeSaveData data = JsonSerializer.Deserialize<MoleculeSaveData>(jsonText);

                List<VisualElement> spawnedAtoms = new List<VisualElement>();

                // 1. Восстанавливаем атомы
                foreach (var savedAtom in data.Atoms)
                {
                    if (VisualElemen.Database.ContainsKey(savedAtom.ElementType))
                    {
                        VisualElemen baseEl = VisualElemen.Database[savedAtom.ElementType];
                        VisualElement newAtom = new VisualElement(baseEl, worldX + savedAtom.OffsetX, worldY + savedAtom.OffsetY);

                        // Делаем их "взрослыми", чтобы физика не разорвала их при старте
                        newAtom.FramesAlive = 61;

                        spawnedAtoms.Add(newAtom);
                        activeElements.Add(newAtom);
                    }
                    else
                    {
                        spawnedAtoms.Add(null); // Если тип не найден, ставим заглушку для сохранения порядка индексов
                    }
                }

                // 2. Восстанавливаем связи
                if (data.Bonds != null)
                {
                    foreach (var bond in data.Bonds)
                    {
                        if (bond.AtomIndex1 < spawnedAtoms.Count && bond.AtomIndex2 < spawnedAtoms.Count)
                        {
                            VisualElement a = spawnedAtoms[bond.AtomIndex1];
                            VisualElement b = spawnedAtoms[bond.AtomIndex2];

                            if (a != null && b != null && !a.Bonds.Contains(b))
                            {
                                a.Bonds.Add(b);
                                b.Bonds.Add(a);
                            }
                        }
                    }
                }

                MyValue = activeElements.Count;
                this.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения файла: " + ex.Message);
            }
        }


        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            PointF worldPos = ScreenToWorld(e.X, e.Y);

            // Клик средней кнопкой мыши или правой по пустому месту - движение камеры
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Right && ModifierKeys == Keys.Shift))
            {
                isPanning = true;
                lastMousePos = e.Location;
                return;
            }

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
            // --- РЕЖИМ УДАЛЕНИЯ ---
            if (e.Button == MouseButtons.Left && isDeleteMode && clickedElement != null)
            {
                // Проверяем, зажат ли Ctrl для удаления всей молекулы
                if (ModifierKeys == Keys.Control)
                {
                    // УДАЛЕНИЕ ВСЕЙ МОЛЕКУЛЫ (Ctrl + Клик)
                    List<VisualElement> moleculeToDelete = GetFullMolecule(clickedElement);
                    foreach (var el in moleculeToDelete)
                    {
                        foreach (var activeEl in activeElements) activeEl.Bonds.Remove(el);
                        el.Bonds.Clear();
                        activeElements.Remove(el);
                    }
                }
                else
                {
                    // УДАЛЕНИЕ ПО ОДНОМУ АТОМУ (Простой клик)
                    // Сначала удаляем ссылки на этот атом у соседей
                    foreach (var neighbor in clickedElement.Bonds)
                    {
                        neighbor.Bonds.Remove(clickedElement);
                    }
                    // Затем удаляем сам атом
                    activeElements.Remove(clickedElement);
                }

                if (draggedMolecule.Contains(clickedElement)) draggedMolecule.Clear();
                if (draggedAtom == clickedElement) draggedAtom = null;

                MyValue = activeElements.Count;
                this.Invalidate();
                return;
            }

            // --- ВЗАИМОДЕЙСТВИЕ С АТОМОМ (Захват / Сохранение) ---
            if (clickedElement != null)
            {
                if (e.Button == MouseButtons.Left)
                {
                    // ЛОГИКА ДВОЙНОГО КЛИКА
                    if (isDoubleClick)
                    {
                        // Это второй клик подряд! Берем всю молекулу
                        isDragging = true;
                        draggedMolecule = GetFullMolecule(clickedElement);
                        draggedAtom = null;

                        foreach (var atom in draggedMolecule)
                        {
                            atom.VX = 0;
                            atom.VY = 0;
                        }

                        isDoubleClick = false;
                        doubleClickTimer.Stop();
                    }
                    else
                    {
                        // Это первый клик. Берем один атом
                        isDragging = true;
                        draggedMolecule.Clear();
                        draggedAtom = clickedElement;
                        draggedAtom.VX = 0;
                        draggedAtom.VY = 0;

                        // Запускаем таймер ожидания второго клика
                        isDoubleClick = true;
                        doubleClickTimer.Start();
                    }

                    lastMousePos = e.Location;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    // ПКМ по атому сохраняет всю молекулу
                    List<VisualElement> molecule = GetFullMolecule(clickedElement);
                    SaveMoleculeToFile(molecule);
                }
                return;
            }

            // Спавн нового атома
            if (e.Button == MouseButtons.Left && selectedElementToPlace != null && VisualElemen.Database.ContainsKey(selectedElementToPlace))
            {
                VisualElemen baseEl = VisualElemen.Database[selectedElementToPlace];
                activeElements.Add(new VisualElement(baseEl, worldPos.X, worldPos.Y));
                MyValue = activeElements.Count;
                this.Invalidate();
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
            else if (isDragging)
            {
                float deltaX = (e.X - lastMousePos.X) / zoom;
                float deltaY = (e.Y - lastMousePos.Y) / zoom;

                if (draggedMolecule.Count > 0)
                {
                    // Тащим всю молекулу (Двойной клик)
                    foreach (var atom in draggedMolecule)
                    {
                        atom.X += deltaX;
                        atom.Y += deltaY;
                        atom.VX = 0;
                        atom.VY = 0;
                    }
                }
                else if (draggedAtom != null)
                {
                    // Тащим один атом (Одинарный клик)
                    draggedAtom.X += deltaX;
                    draggedAtom.Y += deltaY;
                    draggedAtom.VX = 0;
                    draggedAtom.VY = 0;
                }

                lastMousePos = e.Location;
                this.Invalidate();
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            isPanning = false;

            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                draggedMolecule.Clear();
                draggedAtom = null;
            }
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
                        // Игнорируем физику для перетаскиваемых атомов
                        if (isDragging && (draggedMolecule.Contains(el) || el == draggedAtom))
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
                    spatialGrid.GetNearby(a, nearbyBuffer);

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

            // Отрисовка линий химических связей
            using (Pen bondPen = new Pen(Color.DarkGray, 4f))
            {
                foreach (var el in activeElements)
                {
                    foreach (var bonded in el.Bonds)
                    {
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

        private void label2_Click(object sender, EventArgs e) { }
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

    public class AtomSaveData
    {
        public string ElementType { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
    }

    public class BondSaveData
    {
        public int AtomIndex1 { get; set; }
        public int AtomIndex2 { get; set; }
    }

    public class MoleculeSaveData
    {
        public List<AtomSaveData> Atoms { get; set; } = new List<AtomSaveData>();
        public List<BondSaveData> Bonds { get; set; } = new List<BondSaveData>();
    }
}