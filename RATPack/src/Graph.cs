/*
 * Copyright 2015 SatNet
 * 
 * This file is subject to the included LICENSE.md file. 
 */

using System;
using UnityEngine;

namespace RATPack
{
	/// <summary>
	/// Data access function takes an integer and returns a corresponding double.
	/// </summary>
	public delegate double DataAccessFunc(int x);

	/// <summary>
	/// Graph holds a 2D image and provides methods for graphing data within it.
	/// </summary>
	public class Graph
	{
		private int 		_lineWidth 	= 2;
		private Color 		_background = Color.black;
		private Color 		_zeroLine 	= Color.yellow;
		private Color 		_lineColor 	= Color.red;
		private Texture2D 	_image 		= null;
		private bool 		_dirty 		= true;

		/// <summary>
		/// Initializes a new instance of the <see cref="Graph"/> class.
		/// </summary>
		/// <param name="width">Width of the image.</param>
		/// <param name="height">Height of the image.</param>
		public Graph(int width, int height)
		{
			_image = new Texture2D (width, height);
		}
		/// <summary>
		/// Gets or sets the width of the line.
		/// </summary>
		/// <value>The width of the line.</value>
		public int LineWidth
		{
			get { return _lineWidth; }
			set { _lineWidth = value; }
		}
		/// <summary>
		/// Gets or sets the color of the background.
		/// </summary>
		/// <value>The color of the background.</value>
		public Color BackgroundColor
		{
			get { return _background; }
			set { _background = value; }
		}
		/// <summary>
		/// Gets or sets the color of the zero line.
		/// </summary>
		/// <value>The color of the zero line.</value>
		public Color ZeroLineColor
		{
			get { return _zeroLine; }
			set { _zeroLine = value; }
		}
		/// <summary>
		/// Gets or sets the color of the line.
		/// </summary>
		/// <value>The color of the line.</value>
		public Color LineColor
		{
			get { return _lineColor; }
			set { _lineColor = value; }
		}
		/// <summary>
		/// Gets the image.
		/// </summary>
		/// <returns>The image.</returns>
		public Texture2D getImage()
		{
			if (_dirty)
				Apply ();
			return _image;
		}
		/// <summary>
		/// Resize the image to the specified width and height.
		/// </summary>
		/// <param name="width">Width.</param>
		/// <param name="height">Height.</param>
		public void resize(int width, int height)
		{
			_dirty = true;
			_image.Resize(width, height);
		}
		/// <summary>
		/// Reset the image.
		/// </summary>
		/// This should be called prior to adding any lines.
		public void reset()
		{
			_dirty = true;
			for (int y = 0; y < _image.height; y++) {
				for (int x = 0; x < _image.width; x++) {
					_image.SetPixel (x, y, _background);
				}
			}
		}
		/// <summary>
		/// Apply any changes to the image.
		/// </summary>
		public void Apply()
		{
			_image.Apply ();
			_dirty = false;
		}
		/// <summary>
		/// Draws the graph. This single call updates the graph completely.
		/// </summary>
		/// Given a data access function which will return a double value for a given integer index of 0-entries this funtion will create 
		/// a graph of the data using the color scheme previously set. This is a convenience method to simplify creating a graph with just
		/// one data line and a zero line.
		/// <param name="accessFunc">Access function which gives this method access to the data to graph.</param>
		/// <param name="max">Maximum value to be graphed.</param>
		/// <param name="min">Minimum value to be graphed.</param>
		/// <param name="entries">Number of entries accessible via the access function.</param>
		public void drawGraph(DataAccessFunc accessFunc, double max, double min, int entries)
		{
			reset ();
			drawZeroLine (max, min, _zeroLine);
			drawLineOnGraph (accessFunc, max, min, entries, _lineColor);
			Apply ();
		}
		/// <summary>
		/// Draws the zero line.
		/// </summary>
		/// Using the maximum and minimum it determines where zero falls using the graph scale and draws it with the specified color.
		/// <param name="max">Maximum value of the graphed data.</param>
		/// <param name="min">Minimum value of the graphed data.</param>
		/// <param name="color">Color of the zero line.</param>
		public void drawZeroLine(double max, double min, Color color)
		{
			_dirty = true;
			int zeroLine = -1;
			if (min < 0 && max > 0) {
				double scale = (double)_image.height / (max - min);
				zeroLine = (int)(-min * scale);

				if (zeroLine > 0) {
					for (int lx = 0; lx < _image.width; lx++) {

						for (int zy = zeroLine; zy < zeroLine + _lineWidth; zy++) {
							_image.SetPixel (lx, zy, color);
						}
					}
				}
			}
		}

		/// <summary>
		/// Draws a line on graph.
		/// </summary>
		/// <param name="accessFunc">Access function which gives this method access to the data to graph.</param>
		/// <param name="max">Maximum value to be graphed.</param>
		/// <param name="min">Minimum value to be graphed.</param>
		/// <param name="entries">Number of entries accessible via the access function.</param>
		/// <param name="color">Color of this line.</param>
		public void drawLineOnGraph(DataAccessFunc accessFunc, double max, double min, int entries, Color color)
		{
			_dirty = true;
			double scale = (double)_image.height / (max - min);
			int average = 1;
			int remainder = 0;
			int offset = 0;
			// If there are more entries than the _image can display calculate how many entries we should average.
			// If the division is not even calculate how many entries we should skip over and how often.
			if (entries > _image.width) {
				average = entries / _image.width;
				remainder = entries % _image.width;
				if (remainder > 0) {
					offset = (int)Math.Ceiling ((double)_image.width / (double)remainder);
				}
			}

			int prevY = 0;
			int index = 0;
			// For every x determine the range of ys that need to change and set them.
			for (int lx = 0; lx < _image.width;lx++) {
				double value = 0;

				// Don't exceed the boundaries of accessFunc.
				if (index + average > entries) {
					break;
				}
				// Average some values if we have more values than we have width.
				for (int i = 0; i < average; i++) {
					double data = accessFunc (index);
					if (Double.IsNaN (data)) {
						value = data;
					}
					value += data;
					index++;
				}
				if (Double.IsNaN (value)) {
					continue;
				}
				value /= average;

				// If we have too many entries and we can't easily average them drop values, but do it evenly so the holes aren't obvious.
				if (offset > 0 && lx % offset == 0 && remainder > 0) {
					index++;
					remainder--;
				}

				// Subtract min from the value to position it relative to y=0 and scale it to fit the image height. Cast it to an int.
				int y = (int)((value - min )* scale);

				// Calculate a contigous line from the previous y to the current position. This gives the graph an unbroken appearance.
				int startY = prevY;
				int endY = y;
				if (y < prevY) {
					startY = y;
					endY = prevY;
				}
				if (lx == 0) {
					endY = y;
					startY = y;
				}
				// Make sure the line is tall enough to be visible.
				if ((endY - startY) < _lineWidth) {
					endY += (_lineWidth - (endY - startY));
				}
				// Never exceed the image height. We're dealing with floating point, so there is a certain inaccuracy in the scaling that
				// may allow our calculated y to exceed the image height by a pixel or two.
				if (endY > _image.height) {
					endY = _image.height;
					startY = _image.height - _lineWidth;
				}

				// Draw the line segment.
				for (int ly = startY; ly < endY; ly++)
					_image.SetPixel(lx,ly,color);

				prevY = y;
			}
		}

		public void drawVerticalLine(int x, Color color, int height = 0)
		{
			if (height == 0)
				height = _image.height;
			for (int y = 0; y < height; y++) {
				_image.SetPixel (x, y, color);
				_image.SetPixel (x+1, y, color);
			}
		}

		public void drawHorizontalLine(int y, Color color, int width = 0)
		{
			if (width == 0)
				width = _image.width;
			for (int x = 0; x < width; x++) {
				_image.SetPixel (x, y, color);
				_image.SetPixel (x, y+1, color);
			}
		}
	}
}