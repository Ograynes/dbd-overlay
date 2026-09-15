using System.Drawing;
using DBDOverlay.Core.ImageProcessing;
using Xunit;

namespace DBDOverlay.Tests
{
    public class HudLabelLayoutTests
    {
        [Theory]
        [InlineData(1920,1080,4,1)]
        [InlineData(3440,1440,4,1)]
        [InlineData(2560,1440,8,1.5)]
        public void LabelsStayCenteredOnCalibratedRowsWithScreenOffsets(int width,int height,int count,double dpi)
        {
            var client = new Rectangle(-400,80,width,height);
            var geometry = new HudGeometry(new RectangleF(.043f,.40f,.013f,.285f));
            var cells = geometry.Cells(client,count);
            var bounds = HudLabelLayout.Bounds(geometry,client,count,dpi,dpi);
            for(int i=0;i<count;i++)
            {
                double center = (bounds.Top + bounds.Height * (i+.5)/count)*dpi;
                Assert.InRange(System.Math.Abs(center-(cells[i].Top+cells[i].Height/2.0)),0,1.1);
            }
            Assert.InRange(System.Math.Abs((bounds.Left+.45*bounds.Width)*dpi-(cells[0].Left+cells[0].Width/2.0)),0,.01);
        }
    }
}
