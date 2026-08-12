using System;
using System.Collections.Generic;
using static chemesry.Form1;

namespace chemesry
{
    public enum ElementType
    {
        Metal,
        NonMetal,
        Metalloid
    }

    public class Element
    {
        public string Name { get; set; }
        public int Z { get; set; }
        public double A { get; set; }
        public double? X { get; set; }
        public double? IE { get; set; }
        public double? EA { get; set; }
        public double? R { get; set; }
        public List<int> Valence { get; set; }
        public List<int> OxidationState { get; set; }
        public ElementType Type { get; set; }

        public Element(string name, int z, double a, double? x, double? ie, double? ea, double? r, List<int> valence, List<int> oxidationState, ElementType type)
        {
            Name = name; Z = z; A = a; X = x; IE = ie; EA = ea; R = r; Valence = valence; OxidationState = oxidationState; Type = type;
        }

        // Статическая база данных всех твоих элементов
        public static readonly Dictionary<string, Element> Database = new Dictionary<string, Element>
        {
            { "H", new Element("H", 1, 1.008, 2.20, 1312, 73, 37, new List<int> { 1 }, new List<int> { -1, 1 }, ElementType.NonMetal) },
            { "He", new Element("He", 2, 4.0026, null, 2372, null, 31, new List<int> { 0 }, new List<int> { 0 }, ElementType.NonMetal) },
            { "C", new Element("C", 6, 12.011, 2.55, 1086, 122, 77, new List<int> { 2, 4 }, new List<int> { -4, -2, 0, 2, 4 }, ElementType.NonMetal) },
            { "N", new Element("N", 7, 14.007, 3.04, 1402, -7, 75, new List<int> { 3, 4 }, new List<int> { -3, -2, -1, 1, 2, 3, 4, 5 }, ElementType.NonMetal) },
            { "O", new Element("O", 8, 15.999, 3.44, 1314, 141, 73, new List<int> { 2 }, new List<int> { -2, -1, 1, 2 }, ElementType.NonMetal) },
            { "Na", new Element("Na", 11, 22.990, 0.93, 496, 53, 186, new List<int> { 1 }, new List<int> { 1 }, ElementType.Metal) },
            { "Si", new Element("Si", 14, 28.085, 1.90, 786, 134, 111, new List<int> { 4 }, new List<int> { -4, 4 }, ElementType.Metalloid) },
            { "Cl", new Element("Cl", 17, 35.45, 3.16, 1251, 349, 99, new List<int> { 1, 3, 5, 7 }, new List<int> { -1, 1, 3, 5, 7 }, ElementType.NonMetal) },
            { "Ca", new Element("Ca", 20, 40.078, 1.00, 590, 2, 197, new List<int> { 2 }, new List<int> { 2 }, ElementType.Metal) },
            { "Fe", new Element("Fe", 26, 55.845, 1.83, 762, 16, 126, new List<int> { 2, 3, 6 }, new List<int> { -2, 2, 3, 6 }, ElementType.Metal) }
        };
    }

    public class VisualElement
    {
        public Element BaseElement { get; set; }

        public int FramesAlive { get; set; } = 0;

        // Физика (координаты и скорость)
        public float X { get; set; } 
        public float Y { get; set; }
        public float VX { get; set; } // Скорость по оси X
        public float VY { get; set; } // Скорость по оси Y
        public float Radius { get; set; } = 20f; // Размер кружочка
        
        

        // Зачаток для связей
        public List<VisualElement> Bonds { get; set; } = new List<VisualElement>();

        public VisualElement(Element baseElement, float x, float y)
        {
            BaseElement = baseElement;
            X = x;
            Y = y;

           



            // Задаем случайную начальную скорость
            System.Random rnd = new System.Random();
            VX = (float)(rnd.NextDouble() * 4 - 2);
            VY = (float)(rnd.NextDouble() * 4 - 2);
        }

        // Метод отрисовки самого себя
        public void Draw(Graphics g)
        {
            // 1. Отрисовка линий связей (пока пустая, но заготовка работает)
            foreach (var bond in Bonds)
            {
                g.DrawLine(Pens.Black, X, Y, bond.X, bond.Y);
            }

            // 2. Отрисовка кружочка (бесцветный, с черным контуром)
            float drawX = X - Radius;
            float drawY = Y - Radius;
            float diameter = Radius * 2;

            g.FillEllipse(Brushes.White, drawX, drawY, diameter, diameter);
            g.DrawEllipse(Pens.Black, drawX, drawY, diameter, diameter);

            // 3. Отрисовка текста (символ элемента)
            using (Font font = new Font("Arial", 12, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(BaseElement.Name, font);
                g.DrawString(BaseElement.Name, font, Brushes.Black, X - textSize.Width / 2, Y - textSize.Height / 2);
            }
        }
    }
}
