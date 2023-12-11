using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Media
{
	partial class MediaPickerImplementation : IMediaPicker
	{
		public bool IsCaptureSupported
			=> false;

		public async Task<FileResult> PickPhotoAsync(MediaPickerOptions options)
			=> new FileResult(await FilePicker.PickAsync(new PickOptions
			{
				FileTypes = FilePickerFileType.Images
			}));

		public Task<FileResult> CapturePhotoAsync(MediaPickerOptions options)
			=> PickPhotoAsync(options);

		public async Task<FileResult> PickVideoAsync(MediaPickerOptions options)
			=> new FileResult(await FilePicker.PickAsync(new PickOptions
			{
				FileTypes = FilePickerFileType.Videos
			}));

		public Task<FileResult> CaptureVideoAsync(MediaPickerOptions options)
			=> PickVideoAsync(options);
	}
}
