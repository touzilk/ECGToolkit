/***************************************************************************
Copyright 2008, Thoraxcentrum, Erasmus MC, Rotterdam, The Netherlands

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Written by Maarten JB van Ettinger.

****************************************************************************/
using System;
using System.Drawing;

namespace ECGConversion
{
	using ECGDemographics;
	using ECGSignals;

	public class ECGDraw
	{
		// variables used for Bitmap output.
		public const float mm_Per_Inch = 25.4f;
		public const float Inch_Per_mm = 1f / mm_Per_Inch;
		public static float uV_Per_Channel
		{
			get
			{
				return _uV_Per_Channel;
			}
			set
			{
				if ((value % 1000) == 0)
					_uV_Per_Channel = value;
			}
		}

		public static float DpiX = 96.0f;
		public static float DpiY = 96.0f;
		private static float _uV_Per_Channel = 3000.0f;
		private static float _mm_Per_GridLine = 5.0f;
		private static float _TextSize = 9.0f;
		public const int DirtSolutionFactor = 20; // max 20!!

		// Colors for drawing.
		public static Color SignalColor = Color.Black;
		public static Color TextColor = Color.Black;
		public static Brush BackColor = Brushes.Transparent;
		public static Color GraphColor = Color.FromArgb(255, 187, 187);

		/// <summary>
		/// Function to write an ECG in to an bitmap. Will only draw stored leads and will not display average beat.
		/// </summary>   
		/// <param name="src">an ECG file to convert</param>
		/// <param name="output">returns bitmap with signals.</param>
		/// <returns>0 on success</returns>
		public static int ToBitmap(IECGFormat src, float mm_Per_s, float mm_Per_mV, out Bitmap output)
		{
			return ToBitmap(src, 4f * mm_Per_Inch, 4f * mm_Per_Inch, mm_Per_s, mm_Per_mV, out output);
		}
		/// <summary>
		/// Function to write an ECG in to an bitmap. Will only draw stored leads and will not display average beat.
		/// </summary>   
		/// <param name="src">an ECG file to convert</param>
		/// <param name="output">returns bitmap with signals.</param>
		/// <returns>0 on success</returns>
		public static int ToBitmap(IECGFormat src, float dpiX, float dpiY, float mm_Per_s, float mm_Per_mV, out Bitmap output)
		{
			DpiX = dpiX; DpiY = dpiY;

			output = null;

			DateTime dtRecordTime = DateTime.MinValue;

			IDemographic dem = src.Demographics;
			if (dem != null)
				dtRecordTime = dem.TimeAcquisition;

			ECGSignals.Signals signals;
			if ((src.Signals == null)
			||  (src.Signals.getSignals(out signals) != 0))
			{
				return 1;
			}

			int nExtraSpace = (int) (_TextSize * DpiY * Inch_Per_mm * .4f);

			// begin: find start and end.
			int nStart	= int.MaxValue,
				nEnd	= int.MinValue;

			for (int lead=0;lead < signals.NrLeads;lead++)
			{
				if (signals[lead] != null)
				{
					nStart = Math.Min(nStart, signals[lead].RhythmStart);
					nEnd = Math.Max(nEnd, signals[lead].RhythmEnd);
				}
			}
			// end: find start and end.

			float
				fPixel_Per_s = mm_Per_s * DpiX * Inch_Per_mm,
				fPixel_Per_uV = mm_Per_mV * DpiY * Inch_Per_mm * 0.001f,
				fLeadYSpace = _uV_Per_Channel * fPixel_Per_uV;

			output = new Bitmap(
				(int) Math.Ceiling((((float) (nEnd - nStart)) / signals.RhythmSamplesPerSecond) * fPixel_Per_s + (0.2f * fPixel_Per_s)) + 1,
				(int) Math.Ceiling(fLeadYSpace * signals.NrLeads) + nExtraSpace + 1);

			DrawECG(Graphics.FromImage(output), signals, dtRecordTime, 0, mm_Per_s, mm_Per_mV, false);

			return 0;
		}

		/// <summary>
		/// Function to draw an ECG on a bitmap.
		/// </summary>
		/// <param name="myBM">Bitmap do draw ECG in.</param>
		/// <param name="signals">Signal data to draw (can change due to resampling).</param>
		/// <param name="dtRecordTime">Start time of recording</param>
		/// <param name="nTime">Start drawing at this sample number.</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// /// <returns>Sample number to start next draw ECG on.</returns>
		public static int DrawECG(Bitmap myBM, ECGSignals.Signals signals, DateTime dtRecordTime, int nTime, float fmm_Per_s, float fmm_Per_mV)
		{
			return DrawECG(Graphics.FromImage(myBM), signals, dtRecordTime, nTime, fmm_Per_s, fmm_Per_mV, false);
		}
		/// <summary>
		/// Function to draw an ECG on a bitmap.
		/// </summary>
		/// <param name="myBM">Bitmap do draw ECG in.</param>
		/// <param name="signals">Signal data to draw (can change due to resampling).</param>
		/// <param name="dtRecordTime">Start time of recording</param>
		/// <param name="nTime">Start drawing at this sample number.</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// <param name="bAllowResample">True if resample of signal is allowed.</param>
		/// <returns>Sample number to start next draw ECG on.</returns>
		public static int DrawECG(Bitmap myBM, ECGSignals.Signals signals, DateTime dtRecordTime, int nTime, float fmm_Per_s, float fmm_Per_mV, bool bAllowResample)
		{
			return DrawECG(Graphics.FromImage(myBM), signals, dtRecordTime, nTime, fmm_Per_s, fmm_Per_mV, bAllowResample);
		}
		/// <summary>
		/// Function to draw an ECG on a bitmap.
		/// </summary>
		/// <param name="myGraphics">The drawing surface.</param>
		/// <param name="signals">Signal data to draw (can change due to resampling).</param>
		/// <param name="dtRecordTime">Start time of recording</param>
		/// <param name="nTime">Start drawing at this sample number.</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// <param name="bAllowResample">True if resample of signal is allowed.</param>
		/// <returns>Sample number to start next draw ECG on.</returns>
		public static int DrawECG(Graphics myGraphics, ECGSignals.Signals signals, DateTime dtRecordTime, int nTime, float fmm_Per_s, float fmm_Per_mV, bool bAllowResample)
		{
			// begin: drawing of ECG.
			float
				fPixel_Per_ms = fmm_Per_s * DpiX * Inch_Per_mm * 0.001f,
				fPixel_Per_uV = fmm_Per_mV * DpiY * Inch_Per_mm * 0.001f,
				fLeadYSpace = _uV_Per_Channel * fPixel_Per_uV,
				fGridY = (DpiY * Inch_Per_mm) * _mm_Per_GridLine;

			int nMinX = 0,
				nMinY = (int) (_TextSize * DpiY * Inch_Per_mm * .4f),
				nMaxX,
				nMaxY;

			if ((myGraphics == null)
			||	(signals == null))
				return 0;

			if (Math.Ceiling((fLeadYSpace * signals.NrLeads) + fGridY) >= (myGraphics.VisibleClipBounds.Height - nMinY))
			{
				fLeadYSpace = (float) Math.Floor(((myGraphics.VisibleClipBounds.Height - nMinY - (fGridY * 2)) / signals.NrLeads) / fGridY) * fGridY;
			}

			DrawGrid(myGraphics, fLeadYSpace, signals.NrLeads, nMinX, nMinY, out nMaxX, out nMaxY);

			return DrawSignal(myGraphics, signals, dtRecordTime, true, nTime, fmm_Per_s, fmm_Per_mV, nMinX, nMinY, nMaxX, nMaxY, fPixel_Per_ms * 1000f, fPixel_Per_uV, fLeadYSpace, bAllowResample);
			// end: drawing of ECG.
		}
		/// <summary>
		/// Enumaration for specific ECG draw types.
		/// </summary>
		public enum ECGDrawType
		{
			None				= 0x00,
			Regular				= 0x01,
			ThreeXFour			= 0x02,
			ThreeXFourPlusOne	= 0x04,
			ThreeXFourPlusThree	= 0x08,
			SixXTwo				= 0x10,
			Median				= 0x20

		}
		/// <summary>
		/// Function to determine the possible draw types.
		/// </summary>
		/// <param name="signals">signal to detemine draw types for</param>
		/// <returns>possible draw types</returns>
		public static ECGDrawType PossibleDrawTypes(ECGSignals.Signals signals)
		{
			ECGDrawType ret = ECGDrawType.None;

			if (signals != null)
			{
				ret = ECGDrawType.Regular;

				if (signals.IsTwelveLeads)
				{
					int start, end;

					signals.CalculateStartAndEnd(out start, out end);

					int length = (int) Math.Round((end - start) / (float)signals.RhythmSamplesPerSecond);

					if (length <= 10)
					{
						ret = ECGDrawType.Regular
							| ECGDrawType.ThreeXFour
							| ECGDrawType.ThreeXFourPlusOne
							| ECGDrawType.ThreeXFourPlusThree
							| ECGDrawType.SixXTwo;
					}

					if ((signals.MedianAVM != 0)
						&&	(signals.MedianSamplesPerSecond != 0)
						&&	(signals.MedianLength != 0))
						ret |= ECGDrawType.Median;
				}
			}

			return ret;
		}
		class ECGDrawSection
		{
			public ECGDrawSection(float fmm_Per_mV, float x, float y, int sps, int length)
			{
				_X = x;
				_Y = y;
				_LeadType = LeadType.Unknown;
				_AVM = 1000.0f;
				_SamplesPerSecond = sps;
				_Start = 0;
				_End = length;
				_Data = null;
				_fmm_Per_mV = fmm_Per_mV;
			}

			public ECGDrawSection(float fmm_Per_mV, float x, float y, LeadType lt, double avm, int sps, int start, int end, short[] data)
			{
				_X = x;
				_Y = y;
				_LeadType = lt;
				_AVM = avm;
				_SamplesPerSecond = sps;
				_Start = start;
				_End = end;
				_Data = data;
				_fmm_Per_mV = fmm_Per_mV;
			}

			private float _X;
			private float _Y;
			private LeadType _LeadType;
			private double _AVM; // AVM in uV
			private int _SamplesPerSecond;
			private int _Start;
			private int _End;
			private short[] _Data;
			private float _fmm_Per_mV;

			public int DrawSignal(Graphics g)
			{
				float fPixel_Per_uV = _fmm_Per_mV * DpiY * Inch_Per_mm * 0.001f;

				Pen myPen = new Pen(SignalColor);

				int length = _End - _Start + 1;

				if (_Data == null)
				{
					int end = (_SamplesPerSecond * _End) / 5;

					if (_End == 1)
					{
						g.DrawLine(myPen, _X, _Y, _X, _Y - (float) (_AVM * fPixel_Per_uV));
						g.DrawLine(myPen, _X, _Y - (float) (_AVM * fPixel_Per_uV), _X + end, _Y - (float) (_AVM * fPixel_Per_uV));
						g.DrawLine(myPen, _X + end, _Y - (float) (_AVM * fPixel_Per_uV), _X + end, _Y);
					}
					else
					{
						int a = ((end * 2) / 10),
							b = ((end * 7) / 10);

						g.DrawLine(myPen, _X, _Y, _X + a, _Y);
						g.DrawLine(myPen, _X + a, _Y, _X + a, _Y - (float) (_AVM * fPixel_Per_uV));
						g.DrawLine(myPen, _X + a, _Y - (float) (_AVM * fPixel_Per_uV), _X + b, _Y - (float) (_AVM * fPixel_Per_uV));
						g.DrawLine(myPen, _X + b, _Y - (float) (_AVM * fPixel_Per_uV), _X + b, _Y);
						g.DrawLine(myPen, _X + b, _Y, _X + end, _Y);
					}
				}
				else
				{
					if (_LeadType != ECGConversion.ECGSignals.LeadType.Unknown)
					{
						Font fontText = new Font("Verdana", _TextSize);
						SolidBrush solidBrush = new SolidBrush(TextColor);

						g.DrawString(
							_LeadType.ToString(),
							fontText,
							solidBrush,
							_X + 4.0f,
							_Y - (1250.0f * fPixel_Per_uV));

						solidBrush.Dispose();
						fontText.Dispose();
					}

					g.DrawLine(myPen, _X, _Y - (1000.0f * fPixel_Per_uV), _X, _Y - (1250.0f * fPixel_Per_uV));

					int t2=1;
					for (;t2 < length;t2++)
					{
						int t1 = t2 - 1;

						short
							y1 = short.MinValue,
							y2 = short.MinValue;

						if ((t1 >= 0)
							&&	(t2 <= (_End - _Start))
							&&	((t1 + _Start) >= 0)
							&&	((t2 + _Start) < _Data.Length))
						{
							y1 = _Data[t1 + _Start];
							y2 = _Data[t2 + _Start];
						}

						if ((y1 != short.MinValue)
							&&	(y2 != short.MinValue))
						{
							g.DrawLine(
								myPen,
								_X + t1,
								_Y - (float) (y1 * _AVM * fPixel_Per_uV),
								_X + t2,
								_Y - (float) (y2 * _AVM * fPixel_Per_uV));
						}
					}

					t2--;

					g.DrawLine(myPen, _X + t2, _Y - (1000.0f * fPixel_Per_uV), _X + t2, _Y - (1250.0f * fPixel_Per_uV));
				}

				myPen.Dispose();

				return _End;
			}
		}
		/// <summary>
		/// Function to draw an ECG using a certain draw type.
		/// </summary>
		/// <param name="myGraphics">graphics object to draw with.</param>
		/// <param name="signals">signal data.</param>
		/// <param name="dt">Draw type of ECG.</param>
		/// <param name="nTime">Start drawing at this sample number.</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// <param name="bAllowResample">True if resample of signal is allowed.</param>
		/// <returns>Sample number to start next draw ECG on.</returns>
		public static int DrawECG(Graphics myGraphics, ECGSignals.Signals signals, ECGDrawType dt, int nTime, float fmm_Per_s, float fmm_Per_mV, bool bAllowResample)
		{
			if (myGraphics == null)
				return -1;

			if (signals == null)
				return -2;

			if ((PossibleDrawTypes(signals) & dt) == 0)
				return -3;

			int ret = 0;

			float
				fPixel_Per_ms = fmm_Per_s * DpiX * Inch_Per_mm * 0.001f,
				fPixel_Per_s = fmm_Per_s * DpiX * Inch_Per_mm, 
				fPixel_Per_uV = fmm_Per_mV * DpiY * Inch_Per_mm * 0.001f,
				fLeadYSpace = _uV_Per_Channel * fPixel_Per_uV,
				fGridX = (DpiX * Inch_Per_mm) * _mm_Per_GridLine,
				fGridY = (DpiY * Inch_Per_mm) * _mm_Per_GridLine;

			int nMinX = 0,
				nMinY = 0,
				nMaxX = 52,
				nMaxY = 32;

			ECGDrawSection[] drawSections = null;

			int nOldSamplesPerSecond = 0;

			// begin: resample signals to fit. (using a dirty solution)
			if (((dt != ECGDrawType.Median)
			&&	 (signals.RhythmSamplesPerSecond != (int) fPixel_Per_s))
			||	((dt == ECGDrawType.Median)
			&&	 (signals.MedianSamplesPerSecond != (int) fPixel_Per_s)))
			{
				if (!bAllowResample)
				{
					nOldSamplesPerSecond = (dt == ECGDrawType.Median) ? signals.MedianSamplesPerSecond : signals.RhythmSamplesPerSecond;
					signals = signals.Clone();
				}

				// You should note that if signal is already resampled, but dpi changes the new signal
				// will be resampled based on already resampled information instead of reloading from
				// orignal.
				int RI = ECGConversion.ECGTool.ResampleInterval;
				ECGConversion.ECGTool.ResampleInterval /= DirtSolutionFactor;
				for (int i=0;i < signals.NrLeads;i++)
				{
					ECGTool.ResampleLead(signals[i].Rhythm, (int) signals.RhythmSamplesPerSecond * DirtSolutionFactor, (int) (fPixel_Per_s * DirtSolutionFactor), out signals[i].Rhythm);

					if ((signals.MedianSamplesPerSecond != 0)
						&&	(signals[i].Median != null))
						ECGTool.ResampleLead(signals[i].Median, (int) signals.MedianSamplesPerSecond * DirtSolutionFactor, (int) (fPixel_Per_s * DirtSolutionFactor), out signals[i].Median);

					signals[i].RhythmStart = (signals[i].RhythmStart * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);
					signals[i].RhythmEnd = (signals[i].RhythmEnd * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);
				}
				ECGConversion.ECGTool.ResampleInterval = RI;

				nTime = (nTime * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);

				// set new rhythm per samples.
				signals.RhythmSamplesPerSecond = (int) fPixel_Per_s;
				if (signals.MedianSamplesPerSecond != 0)
					signals.MedianSamplesPerSecond = (int) fPixel_Per_s;
			}

			switch (dt)
			{
				case ECGDrawType.Regular:
				{
					nMinY = (int) (_TextSize * DpiY * Inch_Per_mm * .4f);

					// begin: drawing of ECG.
					if (Math.Ceiling(fLeadYSpace * signals.NrLeads) >= (myGraphics.VisibleClipBounds.Height - nMinY))
					{
						fLeadYSpace = (float) Math.Floor(((myGraphics.VisibleClipBounds.Height - nMinY - fGridY) / signals.NrLeads) / fGridY) * fGridY;
					}

					DrawGrid(myGraphics, fLeadYSpace, signals.NrLeads, nMinX, nMinY, out nMaxX, out nMaxY);

					ret = DrawSignal(myGraphics, signals, DateTime.MinValue, false, nTime, fmm_Per_s, fmm_Per_mV, nMinX, nMinY, nMaxX, nMaxY, fPixel_Per_ms * 1000f, fPixel_Per_uV, fLeadYSpace, bAllowResample);
				}
					break;
				case ECGDrawType.SixXTwo:
				{
					drawSections = new ECGDrawSection[18];

					int a = nTime,
						b = (5 * signals.RhythmSamplesPerSecond) + nTime,
						c = (10 * signals.RhythmSamplesPerSecond) + nTime;

					drawSections[ 0] = new ECGDrawSection(fmm_Per_mV, 0.0f,  2.5f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 1] = new ECGDrawSection(fmm_Per_mV, 0.0f,  8.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 2] = new ECGDrawSection(fmm_Per_mV, 0.0f, 13.5f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 3] = new ECGDrawSection(fmm_Per_mV, 0.0f, 19.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 4] = new ECGDrawSection(fmm_Per_mV, 0.0f, 24.5f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 5] = new ECGDrawSection(fmm_Per_mV, 0.0f, 30.0f * fGridY, signals.RhythmSamplesPerSecond, 2);

					drawSections[ 6] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX,  2.5f * fGridY, signals[ 0].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 0].Rhythm);
					drawSections[ 7] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX,  8.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 1].Rhythm);
					drawSections[ 8] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX, 13.5f * fGridY, signals[ 2].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 2].Rhythm);
					drawSections[ 9] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX, 19.0f * fGridY, signals[ 3].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 3].Rhythm);
					drawSections[10] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX, 24.5f * fGridY, signals[ 4].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 4].Rhythm);
					drawSections[11] = new ECGDrawSection(fmm_Per_mV,  2 * fGridX, 30.0f * fGridY, signals[ 5].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 5].Rhythm);

					drawSections[12] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX,  2.5f * fGridY, signals[ 6].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 6].Rhythm);
					drawSections[13] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX,  8.0f * fGridY, signals[ 7].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 7].Rhythm);
					drawSections[14] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX, 13.5f * fGridY, signals[ 8].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 8].Rhythm);
					drawSections[15] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX, 19.0f * fGridY, signals[ 9].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 9].Rhythm);
					drawSections[16] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX, 24.5f * fGridY, signals[10].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[10].Rhythm);
					drawSections[17] = new ECGDrawSection(fmm_Per_mV, 27 * fGridX, 30.0f * fGridY, signals[11].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[11].Rhythm);
				}
					break;
				case ECGDrawType.ThreeXFour:
				{
					drawSections = new ECGDrawSection[15];

					int	a = nTime,
						b = (int) (2.5f * signals.RhythmSamplesPerSecond) + nTime,
						c = (5 * signals.RhythmSamplesPerSecond) + nTime,
						d = (int) (7.5f * signals.RhythmSamplesPerSecond) + nTime,
						e = (10 * signals.RhythmSamplesPerSecond) + nTime;

					drawSections[ 0] = new ECGDrawSection(fmm_Per_mV, 0.0f,  5.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 1] = new ECGDrawSection(fmm_Per_mV, 0.0f, 16.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 2] = new ECGDrawSection(fmm_Per_mV, 0.0f, 27.0f * fGridY, signals.RhythmSamplesPerSecond, 2);

					drawSections[ 3] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX,  5.0f * fGridY, signals[ 0].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 0].Rhythm);
					drawSections[ 4] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 16.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 1].Rhythm);
					drawSections[ 5] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 27.0f * fGridY, signals[ 2].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 2].Rhythm);

					drawSections[ 6] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX,  5.0f * fGridY, signals[ 3].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 3].Rhythm);
					drawSections[ 7] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX, 16.0f * fGridY, signals[ 4].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 4].Rhythm);
					drawSections[ 8] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX, 27.0f * fGridY, signals[ 5].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 5].Rhythm);

					drawSections[ 9] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX,  5.0f * fGridY, signals[ 6].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 6].Rhythm);
					drawSections[10] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX, 16.0f * fGridY, signals[ 7].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 7].Rhythm);
					drawSections[11] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX, 27.0f * fGridY, signals[ 8].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 8].Rhythm);

					drawSections[12] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX,  5.0f * fGridY, signals[ 9].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[ 9].Rhythm);
					drawSections[13] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX, 16.0f * fGridY, signals[10].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[10].Rhythm);
					drawSections[14] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX, 27.0f * fGridY, signals[11].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[11].Rhythm);
				}
					break;
				case ECGDrawType.ThreeXFourPlusOne:
				{
					drawSections = new ECGDrawSection[17];

					int	a = nTime,
						b = (int) (2.5f * signals.RhythmSamplesPerSecond) + nTime,
						c = (5 * signals.RhythmSamplesPerSecond) + nTime,
						d = (int) (7.5f * signals.RhythmSamplesPerSecond) + nTime,
						e = (10 * signals.RhythmSamplesPerSecond) + nTime;

					drawSections[ 0] = new ECGDrawSection(fmm_Per_mV, 0.0f,  4.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 1] = new ECGDrawSection(fmm_Per_mV, 0.0f, 12.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 2] = new ECGDrawSection(fmm_Per_mV, 0.0f, 20.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 3] = new ECGDrawSection(fmm_Per_mV, 0.0f, 28.0f * fGridY, signals.RhythmSamplesPerSecond, 2);

					drawSections[ 4] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX,  4.0f * fGridY, signals[ 0].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 0].Rhythm);
					drawSections[ 5] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 12.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 1].Rhythm);
					drawSections[ 6] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 20.0f * fGridY, signals[ 2].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 2].Rhythm);

					drawSections[ 7] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX,  4.0f * fGridY, signals[ 3].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 3].Rhythm);
					drawSections[ 8] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX, 12.0f * fGridY, signals[ 4].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 4].Rhythm);
					drawSections[ 9] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX, 20.0f * fGridY, signals[ 5].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 5].Rhythm);

					drawSections[10] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX,  4.0f * fGridY, signals[ 6].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 6].Rhythm);
					drawSections[11] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX, 12.0f * fGridY, signals[ 7].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 7].Rhythm);
					drawSections[12] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX, 20.0f * fGridY, signals[ 8].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 8].Rhythm);

					drawSections[13] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX,  4.0f * fGridY, signals[ 9].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[ 9].Rhythm);
					drawSections[14] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX, 12.0f * fGridY, signals[10].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[10].Rhythm);
					drawSections[15] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX, 20.0f * fGridY, signals[11].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[11].Rhythm);

					drawSections[16] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 28.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, e, signals[ 1].Rhythm);
				}
					break;
				case ECGDrawType.ThreeXFourPlusThree:
				{
					drawSections = new ECGDrawSection[21];

					int	a = nTime,
						b = (int) (2.5f * signals.RhythmSamplesPerSecond) + nTime,
						c = (5 * signals.RhythmSamplesPerSecond) + nTime,
						d = (int) (7.5f * signals.RhythmSamplesPerSecond) + nTime,
						e = (10 * signals.RhythmSamplesPerSecond) + nTime;

					drawSections[ 0] = new ECGDrawSection(fmm_Per_mV, 0.0f,  2.5f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 1] = new ECGDrawSection(fmm_Per_mV, 0.0f,  8.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 2] = new ECGDrawSection(fmm_Per_mV, 0.0f, 13.5f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 3] = new ECGDrawSection(fmm_Per_mV, 0.0f, 19.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 4] = new ECGDrawSection(fmm_Per_mV, 0.0f, 24.0f * fGridY, signals.RhythmSamplesPerSecond, 2);
					drawSections[ 5] = new ECGDrawSection(fmm_Per_mV, 0.0f, 29.5f * fGridY, signals.RhythmSamplesPerSecond, 2);

					drawSections[ 6] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX,  2.5f * fGridY, signals[ 0].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 0].Rhythm);
					drawSections[ 7] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX,  8.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 1].Rhythm);
					drawSections[ 8] = new ECGDrawSection(fmm_Per_mV,  2.0f * fGridX, 13.5f * fGridY, signals[ 2].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, b, signals[ 2].Rhythm);

					drawSections[ 9] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX,  2.5f * fGridY, signals[ 3].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 3].Rhythm);
					drawSections[10] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX,  8.0f * fGridY, signals[ 4].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 4].Rhythm);
					drawSections[11] = new ECGDrawSection(fmm_Per_mV, 14.5f * fGridX, 13.5f * fGridY, signals[ 5].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, b, c, signals[ 5].Rhythm);

					drawSections[12] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX,  2.5f * fGridY, signals[ 6].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 6].Rhythm);
					drawSections[13] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX,  8.0f * fGridY, signals[ 7].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 7].Rhythm);
					drawSections[14] = new ECGDrawSection(fmm_Per_mV, 27.0f * fGridX, 13.5f * fGridY, signals[ 8].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, c, d, signals[ 8].Rhythm);

					drawSections[15] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX,  2.5f * fGridY, signals[ 9].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[ 9].Rhythm);
					drawSections[16] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX,  8.0f * fGridY, signals[10].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[10].Rhythm);
					drawSections[17] = new ECGDrawSection(fmm_Per_mV, 39.5f * fGridX, 13.5f * fGridY, signals[11].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, d, e, signals[11].Rhythm);

					drawSections[18] = new ECGDrawSection(fmm_Per_mV, 2.0f * fGridX, 19.0f * fGridY, signals[ 1].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, e, signals[ 1].Rhythm);
					drawSections[19] = new ECGDrawSection(fmm_Per_mV, 2.0f * fGridX, 24.0f * fGridY, signals[ 7].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, e, signals[ 7].Rhythm);
					drawSections[20] = new ECGDrawSection(fmm_Per_mV, 2.0f * fGridX, 29.5f * fGridY, signals[10].Type, signals.RhythmAVM, signals.RhythmSamplesPerSecond, a, e, signals[10].Rhythm);
				}
					break;
				case ECGDrawType.Median:
				{
					drawSections = new ECGDrawSection[15];

					int	a = nTime,
						b = ((signals.MedianLength * signals.MedianSamplesPerSecond) / 1000) + nTime + 1;

					drawSections[ 0] = new ECGDrawSection(fmm_Per_mV, 0.0f,  5.0f * fGridY, signals.MedianSamplesPerSecond, 2);
					drawSections[ 1] = new ECGDrawSection(fmm_Per_mV, 0.0f, 16.0f * fGridY, signals.MedianSamplesPerSecond, 2);
					drawSections[ 2] = new ECGDrawSection(fmm_Per_mV, 0.0f, 27.0f * fGridY, signals.MedianSamplesPerSecond, 2);

					drawSections[ 3] = new ECGDrawSection(fmm_Per_mV,  4.0f * fGridX,  5.0f * fGridY, signals[ 0].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 0].Median);
					drawSections[ 4] = new ECGDrawSection(fmm_Per_mV,  4.0f * fGridX, 16.0f * fGridY, signals[ 1].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 1].Median);
					drawSections[ 5] = new ECGDrawSection(fmm_Per_mV,  4.0f * fGridX, 27.0f * fGridY, signals[ 2].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 2].Median);

					drawSections[ 6] = new ECGDrawSection(fmm_Per_mV, 17.0f * fGridX,  5.0f * fGridY, signals[ 3].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 3].Median);
					drawSections[ 7] = new ECGDrawSection(fmm_Per_mV, 17.0f * fGridX, 16.0f * fGridY, signals[ 4].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 4].Median);
					drawSections[ 8] = new ECGDrawSection(fmm_Per_mV, 17.0f * fGridX, 27.0f * fGridY, signals[ 5].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 5].Median);

					drawSections[ 9] = new ECGDrawSection(fmm_Per_mV, 30.0f * fGridX,  5.0f * fGridY, signals[ 6].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 6].Median);
					drawSections[10] = new ECGDrawSection(fmm_Per_mV, 30.0f * fGridX, 16.0f * fGridY, signals[ 7].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 7].Median);
					drawSections[11] = new ECGDrawSection(fmm_Per_mV, 30.0f * fGridX, 27.0f * fGridY, signals[ 8].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 8].Median);

					drawSections[12] = new ECGDrawSection(fmm_Per_mV, 43.0f * fGridX,  5.0f * fGridY, signals[ 9].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[ 9].Median);
					drawSections[13] = new ECGDrawSection(fmm_Per_mV, 43.0f * fGridX, 16.0f * fGridY, signals[10].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[10].Median);
					drawSections[14] = new ECGDrawSection(fmm_Per_mV, 43.0f * fGridX, 27.0f * fGridY, signals[11].Type, signals.MedianAVM, signals.MedianSamplesPerSecond, a, b, signals[11].Median);
				}
					break;
				default:
					ret = -4;
					break;
			}

			if (drawSections != null)
			{
				Font fontText = new Font("Verdana", _TextSize);
				SolidBrush solidBrush = new SolidBrush(TextColor);

				if ((((nMaxY * fGridY) + (_TextSize * DpiY * Inch_Per_mm * .4f)) > myGraphics.VisibleClipBounds.Height)
				||	!DrawGrid(myGraphics, nMinX, nMinY, nMaxX, nMaxY))
					return -5;

				foreach (ECGDrawSection ds in drawSections)
					ret = Math.Max(ret, ds.DrawSignal(myGraphics));

				myGraphics.DrawString(
					fmm_Per_s + "mm/s, " + fmm_Per_mV + "mm/mV",
					fontText,
					solidBrush,
					(nMaxX * fGridX),
					(nMaxY * fGridY),
					new StringFormat(StringFormatFlags.DirectionRightToLeft));

				fontText.Dispose();
				solidBrush.Dispose();
			}

			if (!bAllowResample
			&&  (nOldSamplesPerSecond != 0))
			{
				ret = (int) (((long) ret * nOldSamplesPerSecond) / signals.RhythmSamplesPerSecond);
			}

			return ret;
		}
		/// <summary>
		/// Function that will draw as much grid as is possible.
		/// </summary>
		/// <param name="myGraphics">The drawing surface.</param>
		/// <param name="fLeadYSpace">Space needed for each lead.</param>
		/// <param name="nNrLeads">Number of leads in signal.</param>
		/// <param name="nMinX">Minimal X for drawing.</param>
		/// <param name="nMinY">Minimal Y for drawing.</param>
		/// <param name="nMaxX">returns the maximum X for drawing signal</param>
		/// <param name="nMaxY">returns the maximum Y for drawing signal</param>
		private static void DrawGrid(Graphics myGraphics, float fLeadYSpace, int nNrLeads, int nMinX, int nMinY, out int nMaxX, out int nMaxY)
		{
			// begin: draw grid.
			myGraphics.FillRectangle(BackColor, 0, 0, myGraphics.VisibleClipBounds.Width, myGraphics.VisibleClipBounds.Height);

			float
				fGridX = (DpiX * Inch_Per_mm) * _mm_Per_GridLine,
				fGridY = (DpiY * Inch_Per_mm) * _mm_Per_GridLine;

			int nExtraSpace = nMinY;

			nMaxX = (int) (Math.Floor((myGraphics.VisibleClipBounds.Width - nMinX - 1) / fGridX) * fGridX) + nMinX;
			nMaxY = nMinY;

			float
				fTempY = (float) Math.Round((fLeadYSpace * nNrLeads) / fGridY) * fGridY + fGridY,
				fMaxY = fTempY + nMinY,
				fPenWidth = DpiX * 0.015625f;

			Pen gridPen = new Pen(GraphColor, fPenWidth >= 1.0f ? fPenWidth : 1.0f);

			while (fMaxY < myGraphics.VisibleClipBounds.Height)
			{
				nMaxY = (int) fMaxY;

				// draw vertical lines.
				for (float i=nMinX;i <= (nMaxX + fPenWidth);i+=fGridX)
				{
					int nTempX = (int) Math.Round(i);
					myGraphics.DrawLine(gridPen, nTempX, nMinY, nTempX, nMaxY);
				}

				// draw horizontal lines.
				for (float i=nMinY;i <= (fMaxY + fPenWidth);i+=fGridY)
				{
					int nTempY = (int) Math.Round(i);
					myGraphics.DrawLine(gridPen, nMinX, nTempY, nMaxX, nTempY);
				}

				nMinY = (int) (fMaxY + nExtraSpace);
				fMaxY += (fTempY + nExtraSpace);
			}

			gridPen.Dispose();
			// end: draw grid.
		}
		/// <summary>
		/// Function that will draw as much grid as is possible.
		/// </summary>
		/// <param name="myGraphics">The drawing surface.</param>
		/// <param name="fLeadYSpace">Space needed for each lead.</param>
		/// <param name="nNrLeads">Number of leads in signal.</param>
		/// <param name="nMinX">Minimal X for drawing.</param>
		/// <param name="nMinY">Minimal Y for drawing.</param>
		/// <param name="nBlocksX">returns the maximum X for drawing signal</param>
		/// <param name="nBlocksY">returns the maximum Y for drawing signal</param>
		/// <returns>true if successfull</returns>
		private static bool DrawGrid(Graphics myGraphics, int nMinX, int nMinY, int nBlocksX, int nBlocksY)
		{
			myGraphics.FillRectangle(BackColor, 0, 0, myGraphics.VisibleClipBounds.Width, myGraphics.VisibleClipBounds.Height);

			float
				fGridX = (DpiX * Inch_Per_mm) * _mm_Per_GridLine,
				fGridY = (DpiY * Inch_Per_mm) * _mm_Per_GridLine,
				fPenWidth = DpiX * 0.015625f;

			int nMaxX = nMinX + (int) Math.Round(fGridX * nBlocksX),
				nMaxY = nMinY + (int) Math.Round(fGridY * nBlocksY);

			if ((nMaxX > myGraphics.VisibleClipBounds.Width)
			||	(nMaxY > myGraphics.VisibleClipBounds.Height))
				return false;

			Pen gridPen = new Pen(GraphColor, fPenWidth >= 1.0f ? fPenWidth : 1.0f);
			
			// draw vertical lines.
			for (float i=nMinX;i <= (nMaxX + fPenWidth);i+=fGridX)
			{
				int nTempX = (int) Math.Round(i);
				myGraphics.DrawLine(gridPen, nTempX, nMinY, nTempX, nMaxY);
			}

			// draw horizontal lines.
			for (float i=nMinY;i <= (nMaxY + fPenWidth);i+=fGridY)
			{
				int nTempY = (int) Math.Round(i);
				myGraphics.DrawLine(gridPen, nMinX, nTempY, nMaxX, nTempY);
			}

			return true;
		}
		/// <summary>
		/// Function that will draw as much signal on the image as possible.
		/// </summary>
		/// <param name="myGraphics">The drawing surface.</param>
		/// <param name="signals">signal data.</param>
		/// <param name="dtRecordTime">Recording time of signal.</param>
		/// <param name="bShowTime">true if need to show the time</param>
		/// <param name="nTime">Sample number to start drawing of signal,</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// <param name="nMinX">minimal X value to draw.</param>
		/// <param name="nMinY">minimal Y value to draw.</param>
		/// <param name="nMaxX">maximum X value to draw.</param>
		/// <param name="nMaxY">maximum Y value to draw.</param>
		/// <param name="fPixel_Per_s">pixels per second.</param>
		/// <param name="fPixel_Per_uV">pixels per uV.</param>
		/// <param name="fLeadYSpace">space for each lead.</param>
		/// <param name="bAllowResample">True if resample of signal is allowed.</param>
		/// <returns>Sample number to start next draw ECG on.</returns>
		private static int DrawSignal(Graphics myGraphics, ECGSignals.Signals signals, DateTime dtRecordTime, bool bShowTime, int nTime, float fmm_Per_s, float fmm_Per_mV, int nMinX, int nMinY, int nMaxX, int nMaxY, float fPixel_Per_s, float fPixel_Per_uV, float fLeadYSpace, bool bAllowResample)
		{
			float fGridY = (DpiY * Inch_Per_mm) * _mm_Per_GridLine;

			int nOldRhythmSamplesPerSecond = 0;

			// begin: resample signals to fit. (using a dirty solution)
			if (signals.RhythmSamplesPerSecond != (int) fPixel_Per_s)
			{
				if (!bAllowResample)
				{
					nOldRhythmSamplesPerSecond = signals.RhythmSamplesPerSecond;
					signals = signals.Clone();
				}

				// You should note that if signal is already resampled, but dpi changes the new signal
				// will be resampled based on already resampled information instead of reloading from
				// orignal.
				int RI = ECGConversion.ECGTool.ResampleInterval;
				ECGConversion.ECGTool.ResampleInterval /= DirtSolutionFactor;
				for (int i=0;i < signals.NrLeads;i++)
				{
					ECGTool.ResampleLead(signals[i].Rhythm, (int) signals.RhythmSamplesPerSecond * DirtSolutionFactor, (int) (fPixel_Per_s * DirtSolutionFactor), out signals[i].Rhythm);

					if ((signals.MedianSamplesPerSecond != 0)
					&&	(signals[i].Median != null))
						ECGTool.ResampleLead(signals[i].Median, (int) signals.MedianSamplesPerSecond * DirtSolutionFactor, (int) (fPixel_Per_s * DirtSolutionFactor), out signals[i].Median);

					signals[i].RhythmStart = (signals[i].RhythmStart * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);
					signals[i].RhythmEnd = (signals[i].RhythmEnd * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);
				}
				ECGConversion.ECGTool.ResampleInterval = RI;

				nTime = (nTime * (int) (fPixel_Per_s * DirtSolutionFactor)) / (int) (signals.RhythmSamplesPerSecond * DirtSolutionFactor);

				// set new rhythm per samples.
				signals.RhythmSamplesPerSecond = (int) fPixel_Per_s;
				if (signals.MedianSamplesPerSecond != 0)
					signals.MedianSamplesPerSecond = (int) fPixel_Per_s;
			}
			// end: resample signals to fit. (dirty solution)

			// begin: find start and end.
			int nStart	= int.MaxValue,
				nEnd	= int.MinValue;

			for (int lead=0;lead < signals.NrLeads;lead++)
			{
				if (signals[lead] != null)
				{
					nStart = Math.Min(nStart, signals[lead].RhythmStart);
					nEnd = Math.Max(nEnd, signals[lead].RhythmEnd);
				}
			}
			// end: find start and end.

			int nPulseSpace = (int) Math.Floor(DpiX * Inch_Per_mm * _mm_Per_GridLine) + 1;

			nStart += nTime;

			int nXOffset = nMinX;
			float fYOffset = nMinY;

			DrawLeadHead(myGraphics, signals, dtRecordTime, bShowTime, nTime, nMinX, fYOffset, nMaxX, fmm_Per_s, fmm_Per_mV, fPixel_Per_uV, fLeadYSpace, nPulseSpace, nMinY);

			Pen myPen = new Pen(SignalColor);

			// begin: write rhythm data to image
			for (int n=nTime+1;n <= nEnd;n++)
			{
				for (byte i=0;i < signals.NrLeads;i++)
				{
					int	x1 = n - nStart - 1 + nPulseSpace + nXOffset,
						x2 = n - nStart + nPulseSpace + nXOffset;
					short
						y1 = short.MinValue,
						y2 = short.MinValue;

					if (x2 > nMaxX)
					{
						// check whether there is space for drawing leads below
						if ((int) (fYOffset + ((signals.NrLeads * fLeadYSpace + fGridY) * 2)) < nMaxY)
						{
							nXOffset -= nMaxX - nMinX - nPulseSpace;
							fYOffset += signals.NrLeads * fLeadYSpace + nMinY + fGridY;

							x1 = n - nStart - 1 + nPulseSpace + nXOffset;
							x2 = n - nStart + nPulseSpace + nXOffset;

							DrawLeadHead(myGraphics, signals, dtRecordTime, bShowTime, n-1, nMinX, fYOffset, nMaxX, fmm_Per_s, fmm_Per_mV, fPixel_Per_uV, fLeadYSpace, nPulseSpace, nMinY);
						}
						else
						{
							// no more space so end drawing.
							nEnd = n - 1;
							break;
						}
					}

					if ((n > signals[i].RhythmStart)
					&&	(n <= signals[i].RhythmEnd))
					{
						y1 = signals[i].Rhythm[n-1-signals[i].RhythmStart];
						y2 = signals[i].Rhythm[n-signals[i].RhythmStart];
					}

					if ((y1 != short.MinValue)
					&&	(y2 != short.MinValue))
						myGraphics.DrawLine(
							myPen,
							x1,
							(float) (((i + .5f) * fLeadYSpace) + fGridY - (y1 * signals.RhythmAVM * fPixel_Per_uV) + fYOffset),
							x2,
							(float) (((i + .5f) * fLeadYSpace) + fGridY - (y2 * signals.RhythmAVM * fPixel_Per_uV) + fYOffset));
				}
			}
			// end: write rhythm data to image

			if (nOldRhythmSamplesPerSecond != 0)
			{
				nEnd = (int) (((long) nEnd * nOldRhythmSamplesPerSecond) / signals.RhythmSamplesPerSecond);
			}

			myPen.Dispose();

			return nEnd;
		}
		/// <summary>
		/// Function to draw the head of a signal.
		/// </summary>
		/// <param name="myGraphics">The drawing surface.</param>
		/// <param name="signals">signal data</param>
		/// <param name="dtRecordTime">start time of recording</param>
		/// <param name="bShowTime">true if need to show time</param>
		/// <param name="nTime">sample number currently add.</param>
		/// <param name="nX">x position in image</param>
		/// <param name="fY">y position in image</param>
		/// <param name="nMaxX">maximum x value</param>
		/// <param name="fmm_Per_s">mm per second</param>
		/// <param name="fmm_Per_mV">mm per mV</param>
		/// <param name="fPixel_Per_uV">Pixels per uV</param>
		/// <param name="fLeadYSpace">Space for lead in Y axe.</param>
		/// <param name="nPulseSpace">Space for the calibration pulse (in pixels)</param>
		/// <param name="nExtraSpace">Space for text (in pixels)</param>
		private static void DrawLeadHead(Graphics myGraphics, ECGSignals.Signals signals, DateTime dtRecordTime, bool bShowTime, int nTime, int nX, float fY, int nMaxX, float fmm_Per_s, float fmm_Per_mV, float fPixel_Per_uV, float fLeadYSpace, int nPulseSpace, int nExtraSpace)
		{
			int nY = (int) Math.Round(fY);

			// begin: write tags per lead. 
			Font fontText = new Font("Verdana", _TextSize);
			SolidBrush solidBrush = new SolidBrush(TextColor);

			long lTime = (nTime * 1000) / signals.RhythmSamplesPerSecond;

			DateTime dtTemp = dtRecordTime.AddMilliseconds(lTime);

			if (bShowTime)
			{
				myGraphics.DrawString(
					((dtTemp.Year > 100)
					?	 dtTemp.ToShortDateString() + " "
					:	 "")
					+	dtTemp.Hour.ToString("00") + ":"
					+	dtTemp.Minute.ToString("00") + ":"
					+	dtTemp.Second.ToString("00") + "."
					+	(dtTemp.Millisecond/100).ToString("0"),
					fontText,
					solidBrush,
					nX,
					nY - nExtraSpace);
			}

			myGraphics.DrawString(
				fmm_Per_s + "mm/s, " + fmm_Per_mV + "mm/mV",
				fontText,
				solidBrush,
				nMaxX,
				nY - nExtraSpace,
				new StringFormat(StringFormatFlags.DirectionRightToLeft));

			nY = (int) Math.Round(fY + (DpiY * Inch_Per_mm * _mm_Per_GridLine));

			for (byte i=0;i < signals.NrLeads;i++)
				myGraphics.DrawString(
					(signals[i].Type != ECGConversion.ECGSignals.LeadType.Unknown
					?	signals[i].Type.ToString()
					:	"Channel " + (i + 1)),
					fontText,
					solidBrush,
					nX + nPulseSpace + 2.0f ,
					nY + 2.0f + i * fLeadYSpace);

			fontText.Dispose();
			fontText = new Font("System", _TextSize);

			fontText.Dispose();
			solidBrush.Dispose();
			// end: write tags per lead.

			// begin: draw pulse.
			Pen myPen = new Pen(SignalColor);

			for (int i=0;i < signals.NrLeads;i++)
			{
				int	a = ((nPulseSpace << 1) / 3) + nX,
					b = (int) Math.Round(((i + .5f) * fLeadYSpace) - (1000.0f * fPixel_Per_uV)) + nY,
					c = (int) Math.Round((i + .5f) * fLeadYSpace) + nY;
				
				myGraphics.DrawLine(myPen, a, c, a, b);
				myGraphics.DrawLine(myPen, a, b, nPulseSpace + nX, b);
				myGraphics.DrawLine(myPen, nPulseSpace + nX, c, nPulseSpace + nX, b);
			}

			myPen.Dispose();
			// end: draw pulse.
		}
	}
}