using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.GPU;
using Emgu.CV.Stitching;


namespace WindowsFormsApplication4
{
    public partial class Form1 : Form
    {

        Image<Bgr, byte> image1;
        Image<Bgr, byte> image2;
        Image<Bgr, byte> image3;
        Image<Bgr, byte> image4;
        Image<Bgr, byte> image5;

        public Form1()
        {
            InitializeComponent();
        }

        private  int difference(int x, int y)
        {
            if (x - y > 255)
            {
                return 255;
            }
            else if (x - y < 0)
            {
                return 0;
            }
            else
            {
                return x - y;
            }
        }

        private  Image<Bgr, byte> subtract(Image<Bgr, byte> image1, Image<Bgr, byte> image2)
        {
            //image2 being scaled up , hence it will always be <= image1
            //diff might have one row / column = 0
            Image<Bgr, byte> diff = new Image<Bgr, byte>(image1.Size);
            for (int i = 0; i < image2.Height; i++)
            {
                for (int j = 0; j < image2.Width; j++)
                {
                    diff.Data[i, j, 0] = (byte)difference(image1.Data[i, j, 0], image2.Data[i, j, 0]);
                    diff.Data[i, j, 1] = (byte)difference(image1.Data[i, j, 1], image2.Data[i, j, 1]);
                    diff.Data[i, j, 2] = (byte)difference(image1.Data[i, j, 2], image2.Data[i, j, 2]);
                }
            }
            return diff;
        }

        private  int sum(int x, int y)
        {
            if (x + y > 255)
            {
                return 255;
            }
            else if (x + y < 0)
            {
                return 0;
            }
            else
            {
                return x + y;
            }
        }

        private  Image<Bgr, byte> add(Image<Bgr, byte> image1, Image<Bgr, byte> image2)
        {
            //image2 being scaled up , hence it will always be <= image1
            //diff might have one row / column = 0
            Image<Bgr, byte> add = new Image<Bgr, byte>(image1.Size);
            for (int i = 0; i < image2.Height; i++)
            {
                for (int j = 0; j < image2.Width; j++)
                {
                    add.Data[i, j, 0] = (byte)sum(image1.Data[i, j, 0], image2.Data[i, j, 0]);
                    add.Data[i, j, 1] = (byte)sum(image1.Data[i, j, 1], image2.Data[i, j, 1]);
                    add.Data[i, j, 2] = (byte)sum(image1.Data[i, j, 2], image2.Data[i, j, 2]);
                }
            }
            return add;
        }

        private  Image<Gray, byte>[] gaussian_pyramid(Image<Gray, byte> mask, int levels)
        {
            Image<Gray, byte>[] pyramid = new Image<Gray, byte>[levels];
            pyramid[0] = mask;
            for (int i = 1; i < levels; i++)
            {
                Image<Gray, byte> temp = new Image<Gray, byte>(pyramid[i - 1].Width / 2, pyramid[i - 1].Height / 2);
                temp = pyramid[i - 1].PyrDown();
                pyramid[i] = temp;
            }
            return pyramid;
        }

        private  Image<Bgr, byte>[] laplacian_pyramid(Image<Bgr, byte> image1, int levels)
        {
            Image<Bgr, byte>[] pyramid = new Image<Bgr, byte>[levels];
            pyramid[0] = image1;
            for (int i = 1; i < levels; i++)
            {
                Image<Bgr, byte> temp = new Image<Bgr, byte>(pyramid[i - 1].Width / 2, pyramid[i - 1].Height / 2);
                temp = pyramid[i - 1].PyrDown();
                pyramid[i] = temp;
            }

            Image<Bgr, byte>[] lap = new Image<Bgr, byte>[levels];
            lap[levels - 1] = pyramid[levels - 1];
            for (int i = 0; i < levels - 1; i++)
            {
                Image<Bgr, byte> temp = new Image<Bgr, byte>(pyramid[i].Width / 2, pyramid[i].Height / 2);
                temp = pyramid[i + 1].PyrUp();
                temp = subtract(pyramid[i], temp);
                lap[i] = temp;
            }
            return lap;
        }

        private  Image<Bgr, byte>[] combine_pyramid_mask(Image<Bgr, byte>[] lap_pyramid1, Image<Bgr, byte>[] lap_pyramid2, Image<Gray, byte>[] mask_pyramid)
        {
            Image<Bgr, byte>[] lap_pyramid_comb = new Image<Bgr, byte>[lap_pyramid1.Length];
            for (int k = 0; k < lap_pyramid_comb.Length; k++)
            {
                Image<Bgr, byte> temp = new Image<Bgr, byte>(lap_pyramid1[k].Width, lap_pyramid1[k].Height);
                temp = lap_pyramid1[k];
                lap_pyramid2[k].Copy(temp, mask_pyramid[k]);
                lap_pyramid_comb[k] = temp;
            }
            return lap_pyramid_comb;
        }

        private  Image<Bgr, byte> blend(Image<Bgr, byte> image1, Image<Bgr, byte> image2, Image<Gray, byte> mask, int levels)
        {
            Image<Bgr, byte>[] lap_pyramid1 = laplacian_pyramid(image1, levels);
            Image<Bgr, byte>[] lap_pyramid2 = laplacian_pyramid(image2, levels);

            Image<Gray, byte>[] mask_pyramid = gaussian_pyramid(mask, levels);
            Image<Bgr, byte>[] lap_pyramid_comb = combine_pyramid_mask(lap_pyramid1, lap_pyramid2, mask_pyramid);

            Image<Bgr, byte>[] image = new Image<Bgr, byte>[lap_pyramid1.Length];
            for (int i = lap_pyramid1.Length - 1; i > 0; i--)
            {
                Image<Bgr, byte> temp = new Image<Bgr, byte>(lap_pyramid_comb[i - 1].Width, lap_pyramid_comb[i - 1].Height);
                temp = lap_pyramid_comb[i].PyrUp();
                lap_pyramid_comb[i - 1] = add(lap_pyramid_comb[i - 1], temp);
            }

            //imageBox1.Image = image1;
            //imageBox2.Image = lap_pyramid_comb[0];
            //imageBox3.Image = image2;
            return lap_pyramid_comb[0];
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            imageBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox5.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox6.SizeMode = PictureBoxSizeMode.StretchImage;
            imageBox7.SizeMode = PictureBoxSizeMode.StretchImage;

            image1 = new Image<Bgr, byte>("../../images/DSC_0057.jpg");
            image2 = new Image<Bgr, byte>("../../images/DSC_0058.jpg");
            image3 = new Image<Bgr, byte>("../../images/DSC_0059.jpg");
            image4 = new Image<Bgr, byte>("../../images/DSC_0060.jpg");
            image5 = new Image<Bgr, byte>("../../images/DSC_0061.jpg");

            imageBox1.Image = image1;
            imageBox2.Image = image2;
            imageBox3.Image = image3;
            imageBox4.Image = image4;
            imageBox5.Image = image5;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Stitcher stitcher = new Stitcher(false);
            Image<Bgr, byte>[] sourceImages = new Image<Bgr, byte>[3];
            sourceImages[0] = image1;
            sourceImages[1] = image2;
            sourceImages[2] = image3;
            Image<Bgr, byte> result = stitcher.Stitch(sourceImages);
            imageBox7.Image = result;
        }

        private Image<Bgr, Byte> resetROI(Image<Bgr, Byte> img)
        {
            Image<Gray, byte> imgGray = img.Convert<Gray, byte>();
            Image<Gray, byte> temp = imgGray.CopyBlank();

            Contour<Point> maxContour = null;
            double maxArea = 0;

            for (var contours = imgGray.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                RETR_TYPE.CV_RETR_EXTERNAL); contours != null; contours = contours.HNext)
            {
                if (contours.Area > maxArea)
                {
                    maxArea = contours.Area;
                    maxContour = contours;
                }
            }
            CvInvoke.cvSetImageROI(img, maxContour.BoundingRectangle);
            return img;
        }

        private  Image<Bgr, byte> Match(Image<Bgr, byte> image1, Image<Bgr, byte> image2 , int flag)
        {
            HomographyMatrix homography = null;
            SURFDetector surfDetectorCPU = new SURFDetector(500, false);

            int k = 2;              //number of matches that we want ot find between image1 and image2
            double uniquenessThreshold = 0.8;

            Matrix<int> indices;
            Matrix<byte> mask;

            VectorOfKeyPoint KeyPointsImage1;
            VectorOfKeyPoint KeyPointsImage2;

            Image<Gray, Byte> Image1G = image1.Convert<Gray, Byte>();
            Image<Gray, Byte> Image2G = image2.Convert<Gray, Byte>();

            if (GpuInvoke.HasCuda)      //Using CUDA, the GPUs can be used for general purpose processing (i.e., not exclusively graphics), speed up performance
            {
                Console.WriteLine("Here");
                GpuSURFDetector surfDetectorGPU = new GpuSURFDetector(surfDetectorCPU.SURFParams, 0.01f);

                // extract features from Image1
                using (GpuImage<Gray, Byte> gpuImage1 = new GpuImage<Gray, byte>(Image1G))                                              //convert CPU input image to GPUImage(greyscale)
                using (GpuMat<float> gpuKeyPointsImage1 = surfDetectorGPU.DetectKeyPointsRaw(gpuImage1, null))                          //find key points for image
                using (GpuMat<float> gpuDescriptorsImage1 = surfDetectorGPU.ComputeDescriptorsRaw(gpuImage1, null, gpuKeyPointsImage1)) //calculate descriptor for each key point
                using (GpuBruteForceMatcher<float> matcher = new GpuBruteForceMatcher<float>(DistanceType.L2))                          //create a new matcher object
                {
                    KeyPointsImage1 = new VectorOfKeyPoint();
                    surfDetectorGPU.DownloadKeypoints(gpuKeyPointsImage1, KeyPointsImage1);                                             //copy the Matrix from GPU to CPU

                    // extract features from Image2
                    using (GpuImage<Gray, Byte> gpuImage2 = new GpuImage<Gray, byte>(Image2G))
                    using (GpuMat<float> gpuKeyPointsImage2 = surfDetectorGPU.DetectKeyPointsRaw(gpuImage2, null))
                    using (GpuMat<float> gpuDescriptorsImage2 = surfDetectorGPU.ComputeDescriptorsRaw(gpuImage2, null, gpuKeyPointsImage2))

                    //for each descriptor of each image2 , we find k best matching points and their distances from image1 descriptors

                    using (GpuMat<int> gpuMatchIndices = new GpuMat<int>(gpuDescriptorsImage2.Size.Height, k, 1, true))             //stores indices of k best mathces
                    using (GpuMat<float> gpuMatchDist = new GpuMat<float>(gpuDescriptorsImage2.Size.Height, k, 1, true))            //stores distance of k best matches

                    using (GpuMat<Byte> gpuMask = new GpuMat<byte>(gpuMatchIndices.Size.Height, 1, 1))                              //stores result of comparison
                    using (Stream stream = new Stream())
                    {
                        matcher.KnnMatchSingle(gpuDescriptorsImage2, gpuDescriptorsImage1, gpuMatchIndices, gpuMatchDist, k, null, stream);        //matching descriptors of image2 to image1 and storing the k best indices and corresponding distances

                        indices = new Matrix<int>(gpuMatchIndices.Size);
                        mask = new Matrix<byte>(gpuMask.Size);

                        //gpu implementation of voteForUniquess
                        using (GpuMat<float> col0 = gpuMatchDist.Col(0))
                        using (GpuMat<float> col1 = gpuMatchDist.Col(1))
                        {
                            GpuInvoke.Multiply(col1, new MCvScalar(uniquenessThreshold), col1, stream);     //by setting stream, we perform an Async Task
                            GpuInvoke.Compare(col0, col1, gpuMask, CMP_TYPE.CV_CMP_LE, stream);             //col0 >= 0.8col1 , only then is it considered a good match
                        }

                        KeyPointsImage2 = new VectorOfKeyPoint();
                        surfDetectorGPU.DownloadKeypoints(gpuKeyPointsImage2, KeyPointsImage2);

                        //wait for the stream to complete its tasks
                        //We can perform some other CPU intesive stuffs here while we are waiting for the stream to complete.
                        stream.WaitForCompletion();

                        gpuMask.Download(mask);
                        gpuMatchIndices.Download(indices);

                        if (GpuInvoke.CountNonZero(gpuMask) >= 4)
                        {
                            int nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(KeyPointsImage1, KeyPointsImage2, indices, mask, 1.5, 20);       //count the number of nonzero points in the mask(this stored the comparison result of col0 >= 0.8col1)
                            //we can create a homography matrix only if we have atleast 4 matching points
                            if (nonZeroCount >= 4)
                                homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(KeyPointsImage1, KeyPointsImage2, indices, mask, 2);
                        }

                    }
                }
            }
            else
            {
                Console.WriteLine("No CUDA");
                //extract features from image2
                KeyPointsImage1 = new VectorOfKeyPoint();
                Matrix<float> DescriptorsImage1 = surfDetectorCPU.DetectAndCompute(Image1G, null, KeyPointsImage1);

                //extract features from image1
                KeyPointsImage2 = new VectorOfKeyPoint();
                Matrix<float> DescriptorsImage2 = surfDetectorCPU.DetectAndCompute(Image2G, null, KeyPointsImage2);
                BruteForceMatcher<float> matcher = new BruteForceMatcher<float>(DistanceType.L2);
                matcher.Add(DescriptorsImage1);

                indices = new Matrix<int>(DescriptorsImage2.Rows, k);
                using (Matrix<float> dist = new Matrix<float>(DescriptorsImage2.Rows, k))
                {
                    matcher.KnnMatch(DescriptorsImage2, indices, dist, k, null);
                    mask = new Matrix<byte>(dist.Rows, 1);
                    mask.SetValue(255);
                    Features2DToolbox.VoteForUniqueness(dist, uniquenessThreshold, mask);
                }

                int nonZeroCount = CvInvoke.cvCountNonZero(mask);
                if (nonZeroCount >= 4)
                {
                    nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(KeyPointsImage1, KeyPointsImage2, indices, mask, 1.5, 20);
                    if (nonZeroCount >= 4)
                        homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(KeyPointsImage1, KeyPointsImage2, indices, mask, 2);
                }
            }
            Image<Bgr, Byte> mImage = image1.Convert<Bgr, Byte>();
            Image<Bgr, Byte> oImage = image2.Convert<Bgr, Byte>();
            Image<Bgr, Byte> result = new Image<Bgr, byte>(mImage.Width + oImage.Width, mImage.Height);

            //Image<Bgr, Byte> temp = Features2DToolbox.DrawMatches(image1, KeyPointsImage1, image2, KeyPointsImage2, indices, new Bgr(255, 255, 255), new Bgr(255, 255, 255), mask, Features2DToolbox.KeypointDrawType.DEFAULT);

            if (homography != null)
            {  //draw a rectangle along the projected model
                Rectangle rect = image1.ROI;
                PointF[] pts = new PointF[] {
               new PointF(rect.Left, rect.Bottom),
               new PointF(rect.Right, rect.Bottom),
               new PointF(rect.Right, rect.Top),
               new PointF(rect.Left, rect.Top)};

                homography.ProjectPoints(pts);

                HomographyMatrix origin = new HomographyMatrix();                //I perform a copy of the left image with a not real shift operation on the origin
                origin.SetIdentity();
                origin.Data[0, 2] = 0;
                origin.Data[1, 2] = 0;
                Image<Bgr, Byte> mosaic = new Image<Bgr, byte>(mImage.Width + oImage.Width, mImage.Height * 2);

                Image<Bgr, byte> warp_image = mosaic.Clone();
                mosaic = mImage.WarpPerspective(origin, mosaic.Width, mosaic.Height, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR, Emgu.CV.CvEnum.WARP.CV_WARP_DEFAULT, new Bgr(0, 0, 0));

                warp_image = oImage.WarpPerspective(homography, warp_image.Width, warp_image.Height, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR, Emgu.CV.CvEnum.WARP.CV_WARP_INVERSE_MAP, new Bgr(200, 0, 0));
                Image<Gray, byte> warp_image_mask = oImage.Convert<Gray, byte>();
                warp_image_mask.SetValue(new Gray(255));
                Image<Gray, byte> warp_mosaic_mask = mosaic.Convert<Gray, byte>();
                warp_mosaic_mask.SetZero();
                warp_mosaic_mask = warp_image_mask.WarpPerspective(homography, warp_mosaic_mask.Width, warp_mosaic_mask.Height, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR, Emgu.CV.CvEnum.WARP.CV_WARP_INVERSE_MAP, new Gray(0));

                warp_image.Copy(mosaic, warp_mosaic_mask);
                if (flag == 1)
                {
                    Console.WriteLine("Using Image Blending");
                    return blend(mosaic, warp_image, warp_mosaic_mask, 2);
                }
                else
                {
                    Console.WriteLine("No Image Blending");
                    return mosaic;
                }
            }
            return null;
        }

        public  Image<Bgr, Byte> InvertAndMatch(Image<Bgr, byte> modelImage, Image<Bgr, byte> observedImage , int flag)
        {
            CvInvoke.cvFlip(modelImage, modelImage, FLIP.HORIZONTAL);
            CvInvoke.cvFlip(observedImage, observedImage, FLIP.HORIZONTAL);
            Image<Bgr, Byte> result = Match(observedImage, modelImage , flag);
            Image<Bgr, float> output = result.Convert<Bgr, float>();
            CvInvoke.cvFlip(result, result, FLIP.HORIZONTAL);
            CvInvoke.cvFlip(modelImage, modelImage, FLIP.HORIZONTAL);
            CvInvoke.cvFlip(observedImage, observedImage, FLIP.HORIZONTAL);
            return result;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Image<Bgr, byte> threePlusfour = Match(image3, image4, 0);
            Image<Bgr, byte> threePlusfour_reset = resetROI(threePlusfour);
            imageBox6.Image = threePlusfour_reset;

            Image<Bgr, byte> threePlusfourPlusfive = InvertAndMatch(image5, threePlusfour_reset , 0);
            Image<Bgr, byte> threePlusfourPlusfive_reset = resetROI(threePlusfourPlusfive);
            imageBox6.Image = threePlusfourPlusfive_reset;
            //Image<Bgr, byte> onePlustwo = FindMatchRight(image1, image2);
            //Image<Bgr, float> onePlustwo_reset = resetROI(onePlustwo).Convert<Bgr, float>();

            Image<Bgr, byte> last = InvertAndMatch(image2, threePlusfourPlusfive_reset, 0);
            Image<Bgr, byte> last1 = resetROI(last);
            Image<Bgr, byte> last2 = Match(last1, image1 , 0);
            last2 = resetROI(last2);
            imageBox6.Image = last2;
        }

        private void button3_Click(object sender, EventArgs e)
        {

            Image<Bgr, byte> temp1;
            Image<Bgr, byte> temp2;
            Image<Bgr, byte> temp3;
            Image<Bgr, byte> temp4;
            Image<Bgr, byte> temp5;
            temp1 = new Image<Bgr, byte>("../../images/DSC_0057.jpg");
            temp2 = new Image<Bgr, byte>("../../images/DSC_0058.jpg");
            temp3 = new Image<Bgr, byte>("../../images/DSC_0059.jpg");
            temp4 = new Image<Bgr, byte>("../../images/DSC_0060.jpg");
            temp5 = new Image<Bgr, byte>("../../images/DSC_0061.jpg");



            Image<Bgr, byte> threePlusfour = Match(temp3, temp4, 1);
            Image<Bgr, byte> threePlusfour_reset = resetROI(threePlusfour);
            //imageBox6.Image = threePlusfour_reset;

            Image<Bgr, byte> threePlusfourPlusfive = InvertAndMatch(temp5, threePlusfour_reset, 1);
            Image<Bgr, byte> threePlusfourPlusfive_reset = resetROI(threePlusfourPlusfive);
            //imageBox6.Image = threePlusfourPlusfive_reset;

            Image<Bgr, byte> last = InvertAndMatch(temp2, threePlusfourPlusfive_reset, 1);
            Image<Bgr, byte> last1 = resetROI(last);
            Image<Bgr, byte> last2 = Match(last1, image1, 1);
            last2 = resetROI(last2);
            imageBox7.Image = last2;
        }
    }
}
