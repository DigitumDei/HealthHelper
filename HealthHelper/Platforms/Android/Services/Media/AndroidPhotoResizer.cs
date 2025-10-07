using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Graphics;
using AndroidX.ExifInterface.Media;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Media;

public sealed class AndroidPhotoResizer : IPhotoResizer
{
    private readonly ILogger<AndroidPhotoResizer> _logger;

    public AndroidPhotoResizer(ILogger<AndroidPhotoResizer> logger)
    {
        _logger = logger;
    }

    public Task ResizeAsync(string filePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("ResizeAsync skipped because file {FilePath} does not exist.", filePath);
                    return;
                }

                using var boundsOptions = new BitmapFactory.Options { InJustDecodeBounds = true };
                BitmapFactory.DecodeFile(filePath, boundsOptions);

                if (boundsOptions.OutWidth <= 0 || boundsOptions.OutHeight <= 0)
                {
                    _logger.LogWarning("Unable to determine dimensions for image {FilePath}.", filePath);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                int sampleSize = CalculateInSampleSize(boundsOptions.OutWidth, boundsOptions.OutHeight, maxWidth, maxHeight);
                bool alreadyWithinBounds = sampleSize <= 1 && boundsOptions.OutWidth <= maxWidth && boundsOptions.OutHeight <= maxHeight;
                if (alreadyWithinBounds)
                {
                    _logger.LogDebug("Image {FilePath} already within target bounds. Skipping resize.", filePath);
                    return;
                }

                using var decodeOptions = new BitmapFactory.Options
                {
                    InPreferredConfig = Bitmap.Config.Argb8888,
                    InSampleSize = sampleSize
                };

                cancellationToken.ThrowIfCancellationRequested();

                Bitmap? decodedBitmap = BitmapFactory.DecodeFile(filePath, decodeOptions);
                if (decodedBitmap is null)
                {
                    _logger.LogWarning("Failed to decode bitmap for {FilePath} during resize.", filePath);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                bool orientationAdjusted = false;
                Bitmap? finalBitmap = null;

                try
                {
                    finalBitmap = ApplyOrientationIfNeeded(filePath, decodedBitmap, out orientationAdjusted);

                    using var output = File.Create(filePath);
                    if (!finalBitmap.Compress(Bitmap.CompressFormat.Jpeg, 90, output))
                    {
                        _logger.LogWarning("Bitmap compression returned false for {FilePath}.", filePath);
                    }
                    output.Flush();

                    _logger.LogInformation("Resized photo {FilePath} with sample size {SampleSize}.", filePath, sampleSize);
                }
                finally
                {
                    finalBitmap?.Dispose();
                    if (orientationAdjusted)
                    {
                        decodedBitmap.Dispose();
                    }
                    else
                    {
                        // If no orientation change, finalBitmap equals decodedBitmap and is already disposed.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("ResizeAsync canceled for {FilePath}.", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error resizing image {FilePath}.", filePath);
            }
        }, cancellationToken);
    }

    private static int CalculateInSampleSize(int width, int height, int maxWidth, int maxHeight)
    {
        int inSampleSize = 1;

        if (height <= maxHeight && width <= maxWidth)
        {
            return inSampleSize;
        }

        int halfHeight = height / 2;
        int halfWidth = width / 2;

        while ((halfHeight / inSampleSize) >= maxHeight && (halfWidth / inSampleSize) >= maxWidth)
        {
            inSampleSize *= 2;
        }

        return Math.Max(inSampleSize, 1);
    }

    private static Bitmap ApplyOrientationIfNeeded(string filePath, Bitmap bitmap, out bool orientationAdjusted)
    {
        orientationAdjusted = false;

        using var exif = new ExifInterface(filePath);
        int orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, (int)ExifInterface.OrientationNormal);

        if (orientation == (int)ExifInterface.OrientationNormal || orientation == (int)ExifInterface.OrientationUndefined)
        {
            return bitmap;
        }

        Matrix matrix = new();
        switch (orientation)
        {
            case (int)ExifInterface.OrientationRotate90:
                matrix.PostRotate(90);
                break;
            case (int)ExifInterface.OrientationRotate180:
                matrix.PostRotate(180);
                break;
            case (int)ExifInterface.OrientationRotate270:
                matrix.PostRotate(270);
                break;
            case (int)ExifInterface.OrientationFlipHorizontal:
                matrix.PreScale(-1, 1);
                break;
            case (int)ExifInterface.OrientationFlipVertical:
                matrix.PreScale(1, -1);
                break;
            case (int)ExifInterface.OrientationTranspose:
                matrix.PreScale(-1, 1);
                matrix.PostRotate(90);
                break;
            case (int)ExifInterface.OrientationTransverse:
                matrix.PreScale(-1, 1);
                matrix.PostRotate(270);
                break;
        }

        if (matrix.IsIdentity)
        {
            return bitmap;
        }

        Bitmap transformed = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
        exif.SetAttribute(ExifInterface.TagOrientation, ((int)ExifInterface.OrientationNormal).ToString());
        exif.SaveAttributes();
        orientationAdjusted = true;
        return transformed;
    }
}
