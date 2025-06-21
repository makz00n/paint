using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using static System.Windows.Forms.AxHost;

namespace paint
{
    public partial class Form1 : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        bool paint = false;
        bool stateSaved = false;
        bool saved = false;
        bool changed = false;
        int index = 1;
        int x, y, x1, y1, x2, y2;
        Color color;
        Point pointX, pointY;
        Bitmap bitmapN;
        Graphics graphics;
        Pen pen = new Pen(Color.Black,2);
        Pen erase = new Pen(Color.White, 2);
        ColorDialog colorDialog = new ColorDialog();

        private bool isCutting = false;
        private Point cutStartPoint;
        private Rectangle cutRectangle;
        private Bitmap cutImage;

        private LinkedList<Bitmap> actions = new LinkedList<Bitmap>();
        private LinkedList<Bitmap> del_actions = new LinkedList<Bitmap>();

        private bool isPasting = false;
        private Point pastePosition = Point.Empty;
        private Point pasteStart = Point.Empty;
        private bool draggingPaste = false;


        //private bool ColorsAreSimilar(Color c1, Color c2, int tolerance = 10)
        //{
        //    int rDiff = c1.R - c2.R;
        //    int gDiff = c1.G - c2.G;
        //    int bDiff = c1.B - c2.B;
        //    int aDiff = c1.A - c2.A;

        //    int distance = rDiff * rDiff + gDiff * gDiff + bDiff * bDiff + aDiff * aDiff;
        //    return distance <= tolerance * tolerance;
        //}
        static Point SetPoint(PictureBox pictureBox, Point point)
        {
            float pX = 1f * pictureBox.Image.Width / pictureBox.Width;
            float pY = 1f * pictureBox.Image.Height / pictureBox.Height;
            return new Point((int)(point.X * pX), (int)(point.Y * pY));
        }

        private void Color_Click(object sender, EventArgs e)
        {
            PictureBox picture = (PictureBox)sender;
            CurrColor.BackColor = pen.Color = color = picture.BackColor;
        }

        private void ColorSet(object sender, EventArgs e)
        {
            colorDialog.ShowDialog();
            color = CurrColor.BackColor = pen.Color = colorDialog.Color;
        }

        private void SetPenWidth(object sender, EventArgs e)
        {
            foreach(var btn in panel3.Controls.OfType<Button>())
                btn.BackColor = Color.WhiteSmoke;
            Button button = (Button)sender;
            button.BackColor = Color.LightGreen;
            pen.Width = erase.Width = Convert.ToInt32(button.Tag);
        }

        private void btn_Click(object sender, EventArgs e)
        {
            foreach (var btn in tableLayoutPanel1.Controls.OfType<Button>())
                btn.BackColor = Color.WhiteSmoke;
            Button button = (Button)sender;
            button.BackColor = Color.LightGreen;
            index = Convert.ToInt32(button.Tag);
            isCutting = (index == 8);
            if (index == 9)
            {
                PasteImage();
                return;
            }
            if(isPasting)
            {
                graphics.DrawImage(cutImage, pastePosition);
                SaveState();
                isPasting = false;
                cutImage = null;
                pictureBox1.Invalidate();
            }
            if (!isCutting)
            {
                cutRectangle = new Rectangle(0,0,0,0);
                pictureBox1.Invalidate();  
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            paint = true;
            pointY = e.Location;
            x2 = e.X;
            y2 = e.Y;
            stateSaved = false;
            if (isCutting)
            {
                cutStartPoint = e.Location;
                cutRectangle = new Rectangle(e.Location, new Size(0, 0)); 
            }
            if (isPasting && cutImage != null)
            {
                Rectangle imgRect = new Rectangle(pastePosition, cutImage.Size);
                if (imgRect.Contains(e.Location))
                {
                    draggingPaste = true;
                    pasteStart = new Point(e.X - pastePosition.X, e.Y - pastePosition.Y);
                }
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            paint = false;
            x1 = x - x2;
            y1 = y - y2;
            if (!stateSaved && (index == 1 || index == 2))
            {
                SaveState();
                stateSaved = true;
            }
            //graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (index == 5)
            {
                graphics.DrawLine(pen, x2, y2, x, y);
                SaveState();
            }
            if(index == 6)
            {
                graphics.DrawRectangle(pen, Math.Min(x2, x), Math.Min(y2, y), Math.Abs(x - x2), Math.Abs(y - y2));
                SaveState();
            }
            if(index == 7)
            {
                graphics.DrawEllipse(pen, x2, y2, x1, y1);
                SaveState();
            }
            pictureBox1.Invalidate();
            if (isCutting)
            {
                cutRectangle.Width = Math.Abs(e.X - cutStartPoint.X);
                cutRectangle.Height = Math.Abs(e.Y - cutStartPoint.Y);

                cutRectangle.X = Math.Min(cutStartPoint.X, e.X);
                cutRectangle.Y = Math.Min(cutStartPoint.Y, e.Y);

                
                if (cutRectangle.Width > 0 && cutRectangle.Height > 0)
                {
                   // SaveState();
                    PerformCut(cutRectangle);
                    SaveState();
                }
                cutRectangle = new Rectangle(0, 0, 0, 0);
                pictureBox1.Invalidate();
            }
            if (isPasting && draggingPaste)
            {
                draggingPaste = false;
                graphics.DrawImage(cutImage, pastePosition);
                SaveState();
                isPasting = false;
                cutImage = null;
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            Point point = SetPoint(pictureBox1, e.Location);
            if (index == 3)
            {
                //SaveState();
                FillUp(bitmapN, point.X, point.Y, color);
                SaveState();
            }
            if (index == 4)
            {
                color = pen.Color = CurrColor.BackColor = ((Bitmap)pictureBox1.Image).GetPixel(point.X, point.Y);
            }
            pictureBox1.Invalidate();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if(paint)
            {
                //if (!stateSaved && (index == 1 || index == 2))
                //{
                //    SaveState();
                //    stateSaved = true;
                //}
                if (index == 1)
                {
                    graphics.SmoothingMode = SmoothingMode.None;
                    pointX = e.Location;
                    int brushSize = (int)pen.Width;

                    int dx = pointX.X - pointY.X;
                    int dy = pointX.Y - pointY.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (distance == 0) distance = 1;


                    int steps = (int)(distance * Math.Max(1.0f, 3.0f / brushSize));

                    using (SolidBrush brush = new SolidBrush(pen.Color))
                    {
                        for (int i = 0; i <= steps; i++)
                        {
                            float t = (float)i / steps;
                            int x = (int)(pointY.X + t * dx);
                            int y = (int)(pointY.Y + t * dy);
                            graphics.FillEllipse(brush, x - brushSize / 2, y - brushSize / 2, brushSize, brushSize);
                        }
                    }

                    pointY = pointX;
                }
                if (index == 2)
                {
                    pointX = e.Location;
                    int eraserSize = (int)pen.Width;
                    graphics.SmoothingMode = SmoothingMode.None;

                    int dx = pointX.X - pointY.X;
                    int dy = pointX.Y - pointY.Y;
                    int distance = (int)Math.Sqrt(dx * dx + dy * dy);

                    if (distance == 0) distance = 1;

                    for (int i = 0; i <= distance; i++)
                    {
                        float t = (float)i / distance;
                        int x = (int)(pointY.X + t * dx);
                        int y = (int)(pointY.Y + t * dy);

                        graphics.FillRectangle(Brushes.White, x - eraserSize / 2, y - eraserSize / 2, eraserSize, eraserSize);
                    }

                    pointY = pointX;
                }
            }
            pictureBox1.Refresh();
            x = e.X;
            y = e.Y;
            x1 = e.X - x2;
            y1 = e.Y - y2;
            if (isCutting && paint)
            {
                
                cutRectangle.Width = Math.Abs(e.X - cutStartPoint.X);
                cutRectangle.Height = Math.Abs(e.Y - cutStartPoint.Y);

                cutRectangle.X = Math.Min(cutStartPoint.X, e.X);
                cutRectangle.Y = Math.Min(cutStartPoint.Y, e.Y);
                pictureBox1.Invalidate(); 
            }
            if (isPasting && draggingPaste)
            {
                pastePosition = new Point(e.X - pasteStart.X, e.Y - pasteStart.Y);
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics graphicsPaint  = e.Graphics;
            if(paint)
            {
                if (index == 5)
                {
                    graphicsPaint.DrawLine(pen, x2, y2, x, y);
                }
                if (index == 6)
                {
                    graphicsPaint.DrawRectangle(pen, Math.Min(x2, x), Math.Min(y2, y), Math.Abs(x - x2), Math.Abs(y - y2));
                }
                if (index == 7)
                {
                    graphicsPaint.DrawEllipse(pen, x2, y2, x1, y1);
                }
            }
            if (isCutting)
            {
                
                using (Pen selectPen = new Pen(Color.Black, 1))
                {
                    selectPen.DashStyle = DashStyle.Dash;
                    graphicsPaint.DrawRectangle(selectPen, cutRectangle);
                }
            }
            if (isPasting && cutImage != null)
            {
                e.Graphics.DrawImage(cutImage, pastePosition);
                using (Pen borderPen = new Pen(Color.Gray))
                {
                    borderPen.DashStyle = DashStyle.Dash;
                    e.Graphics.DrawRectangle(borderPen, new Rectangle(pastePosition, cutImage.Size));
                }
            }
        }

        private void с(object sender, EventArgs e)
        {
            //SaveState();
            graphics.Clear(Color.White);
            pictureBox1.Image = bitmapN;
            foreach (var btn in panel3.Controls.OfType<Button>())
                btn.BackColor = Color.WhiteSmoke;
            foreach (var btn in tableLayoutPanel1.Controls.OfType<Button>())
                btn.BackColor = Color.WhiteSmoke;
            Pencil.BackColor = PenWidth1.BackColor = Color.LightGreen;
            pen.Width = erase.Width = 2;
            index = 1;
            SaveState();
        }

        private void save(object sender, EventArgs e)
        {
            var S = new SaveFileDialog();
            S.Filter = "JPEG Image(*.jpg)|*.jpg|Png Image(*.png)|*.png|Bitmap image(*.bmp)|*.bmp|All files(*.*)|*.*";
            if(S.ShowDialog() == DialogResult.OK)
            {
                Bitmap btm = bitmapN.Clone(new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height), bitmapN.PixelFormat);
                string fileExtension = Path.GetExtension(S.FileName).ToUpper();
                ImageFormat format = ImageFormat.Jpeg;
                if (fileExtension == ".BMP") format = ImageFormat.Bmp;
                if (fileExtension == ".PNG") format = ImageFormat.Png;
                btm.Save(S.FileName, format);
                saved = true;
            }
        }

        private void openfile_Click(object sender, EventArgs e)
        {
            var file = new OpenFileDialog();
            file.Filter = "JPEG Image(*.jpg)|*.jpg|Png Image(*.png)|*.png|Bitmap image(*.bmp)|*.bmp|All files(*.*)|*.*";
            if(file.ShowDialog() == DialogResult.OK)
            {
                Bitmap loadedBitmap = new Bitmap(file.FileName);
                bitmapN = new Bitmap(file.FileName);
                pictureBox1.Image = bitmapN;
                graphics = Graphics.FromImage(bitmapN);
            }
        }

        private void remove(object sender, EventArgs e)
        {
            if (actions.Count > 1)
            {
                del_actions.AddLast(actions.Last());
                actions.RemoveLast();

                Bitmap lastState = new Bitmap(actions.Last());
                bitmapN = new Bitmap(pictureBox1.Width, pictureBox1.Height);
                graphics = Graphics.FromImage(bitmapN);
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.DrawImage(lastState, new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height));
                pictureBox1.Image = bitmapN;
                pictureBox1.Invalidate();
            }
        }

        private void PerformCut(Rectangle rect)
        {

           
            cutImage = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(cutImage))
            {
                g.DrawImage(bitmapN, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }
            Clipboard.SetImage(cutImage);
            
            using (Graphics g = Graphics.FromImage(bitmapN))
            {
                g.FillRectangle(new SolidBrush(Color.White), rect); 
            }
            pictureBox1.Invalidate(); 
        }

        private void PasteImage()
        {
            if (cutImage == null) return;
            isPasting = true;
            pastePosition = new Point(0, 0); 
            pictureBox1.Invalidate();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                return;

            Bitmap newBitmap = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            using (Graphics newGraphics = Graphics.FromImage(newBitmap))
            {
                newGraphics.Clear(Color.White);

                if (bitmapN != null)
                {
                    newGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    newGraphics.SmoothingMode = SmoothingMode.None;

                    newGraphics.DrawImage(bitmapN, new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height));
                }
            }

            bitmapN.Dispose(); 
            bitmapN = newBitmap;
            graphics = Graphics.FromImage(bitmapN);
            pictureBox1.Image = bitmapN;
            pictureBox1.Invalidate();
        }

        private void unremove(object sender, EventArgs e)
        {
            if (del_actions.Count > 0)
            {
                Bitmap redoBitmap = del_actions.Last();
                del_actions.RemoveLast();

                actions.AddLast(new Bitmap(redoBitmap));

                bitmapN = new Bitmap(pictureBox1.Width, pictureBox1.Height);
                graphics = Graphics.FromImage(bitmapN);
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.DrawImage(redoBitmap, new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height));
                pictureBox1.Image = bitmapN;
                pictureBox1.Invalidate();
            }
        }

        private void top_panel(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (saved || !changed) return;
            DialogResult result = MessageBox.Show("Хотите сохранить изображение?", "Внимание", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                save(sender, e); 
            }
            else if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
        }

        public void FillUp(Bitmap bitmap, int x, int y, Color newColor)
        {

            Color oldColor = bitmap.GetPixel(x, y);
            if (oldColor.ToArgb() == newColor.ToArgb()) return;

            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(x, y));

            while (queue.Count > 0)
            {
                Point pt = queue.Dequeue();
                int currentX = pt.X;
                int currentY = pt.Y;

                int left = currentX;
                while (left >= 0 && bitmap.GetPixel(left, currentY).ToArgb() == oldColor.ToArgb())
                {
                    left--;
                }
                left++; 

                
                int right = currentX;
                while (right < bitmap.Width && bitmap.GetPixel(right, currentY).ToArgb() == oldColor.ToArgb())
                {
                    right++;
                }
                right--; 

                
                for (int i = left; i <= right; i++)
                {
                    bitmap.SetPixel(i, currentY, newColor);

                    if (currentY > 0 && bitmap.GetPixel(i, currentY - 1).ToArgb() == oldColor.ToArgb())
                    {
                        queue.Enqueue(new Point(i, currentY - 1));
                    }

                    if (currentY < bitmap.Height - 1 && bitmap.GetPixel(i, currentY + 1).ToArgb() == oldColor.ToArgb())
                    {
                        queue.Enqueue(new Point(i, currentY + 1));
                    }
                }
            }
        }
        public Form1()
        {
            InitializeComponent();
            bitmapN = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            graphics = Graphics.FromImage(bitmapN);
            graphics.Clear(Color.White);
            pictureBox1.Image = bitmapN;
            Pencil.BackColor = PenWidth1.BackColor = Color.LightGreen;
            CurrColor.BackColor = Color.Black;
            color = Color.Black;
            actions.AddLast(new Bitmap(bitmapN));
            graphics.SmoothingMode = SmoothingMode.None;
            this.Resize += Form1_Resize;
        }

        private void Close_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Format_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Maximized; 
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void turn_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void SaveState()
        {
            Bitmap copy = new Bitmap(bitmapN.Width, bitmapN.Height);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(bitmapN, 0, 0);
            }

            actions.AddLast(copy);
            if (actions.Count > 30)
            {
                actions.RemoveFirst();
            }
            del_actions.Clear();
            saved = false;
            changed = true;
        }
    }
}
