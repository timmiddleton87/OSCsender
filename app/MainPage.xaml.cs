using Newtonsoft.Json;
using SharpOSC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;

namespace OSCsender

{

	public partial class MainPage : ContentPage
	{
		// Updated pattern to allow double quotes
		private const string OscAddressPattern = @"^[a-zA-Z0-9/_\-.: ""']+$";

		// Counter to keep track of row numbers
		private int rowCount = 0;

		public MainPage()
		{
			InitializeComponent();
		}

		private void AddButton_Clicked(object sender, EventArgs e)
		{
			// Check number of existing rows
			rowCount = 0;
			foreach (var child in MessageStack.Children)
			{
				if (child is Grid grid)
				{
					rowCount++;
				}
			}
			AddNewRow();
		}


		private void AddNewRow(string message = "", string title = "", int id = 0)
		{
			var row = new Grid
			{
				ColumnDefinitions = new ColumnDefinitionCollection
		{
			new ColumnDefinition { Width = new GridLength(50) }, // For row number
            new ColumnDefinition { Width = new GridLength(250) },
			new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
			new ColumnDefinition { Width = new GridLength(100) },
			new ColumnDefinition { Width = new GridLength(100) }
		}
			};

			int cueId = id;

			if (id == 0)
			{
				rowCount++;
				cueId = rowCount;
			}

			var idLabel = new Label
			{
				Text = cueId.ToString(),
				TextColor = Colors.White,
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.Center
			};
			var titleLabel = new Entry
			{
				Text = title,
				Placeholder = "Cue Title",
				PlaceholderColor = Colors.Gray,
				BackgroundColor = Colors.White,
				Margin = new Thickness(5)
			};
			var textBox = new Entry
			{
				Text = message,
				Placeholder = "OSC Message",
				PlaceholderColor = Colors.Gray,
				BackgroundColor = Colors.White,
				Margin = new Thickness(5)
			};
			textBox.TextChanged += (s, e) =>
			{
				// Handle the text change
				var updatedText = NormalizeQuotes(textBox.Text);
				if (textBox.Text != updatedText)
				{
					textBox.Text = updatedText;
					textBox.CursorPosition = updatedText.Length; // Move cursor to end
				}
			};
			textBox.Focused += (s, e) =>
			{
				// Clear the placeholder text when the user starts typing
				if (textBox.Text == "")
				{
					textBox.Placeholder = "";
				}
			};

			var sendButton = new Button
			{
				Text = "Send",
				BackgroundColor = Colors.Green,
				TextColor = Colors.White,
				Margin = new Thickness(5)
			};
			var removeButton = new Button
			{
				Text = "Remove",
				BackgroundColor = Colors.Red,
				TextColor = Colors.White,
				Margin = new Thickness(5)
			};

			sendButton.Clicked += (s, e) => SendOSCMessage(textBox.Text);
			removeButton.Clicked += (s, e) => RemoveRow(row);

			// ID Label goes in the first column
			Grid.SetColumn(idLabel, 0);
			// Title Label goes in the second column
			Grid.SetColumn(titleLabel, 1);
			// TextBox and buttons go in subsequent columns
			Grid.SetColumn(textBox, 2);
			Grid.SetColumn(sendButton, 3);
			Grid.SetColumn(removeButton, 4);

			row.Children.Add(idLabel);
			row.Children.Add(titleLabel);
			row.Children.Add(textBox);
			row.Children.Add(sendButton);
			row.Children.Add(removeButton);

			MessageStack.Children.Add(row);
		}





		private void ValidateOscInput(Entry textBox)
		{
			var text = textBox.Text;
			if (!Regex.IsMatch(text, OscAddressPattern))
			{
				// Allowing double quotes and spaces in input
				var validText = Regex.Replace(text, "[^a-zA-Z0-9/_\\-.: \"']", string.Empty);
				textBox.Text = validText;
				textBox.CursorPosition = textBox.Text.Length; // Move cursor to end
			}
		}

		private (string addressPattern, List<object> args) ParseOSCMessage(string message)
		{
			var parts = message.Split(new[] { ' ' }, 2);
			if (parts.Length < 2)
			{
				return (message, new List<object>()); // No arguments
			}

			var addressPattern = parts[0];
			var argumentString = parts[1];

			var args = new List<object>();
			var oscFormat = ",";

			// Regular expression to match quoted strings and single words
			var regex = new Regex(@"(?:""([^""]*)""|(\S+))", RegexOptions.Compiled);
			var matches = regex.Matches(argumentString);

			foreach (Match match in matches)
			{
				if (match.Groups[1].Success) // Quoted string
				{
					args.Add(match.Groups[1].Value);
					oscFormat += "s";
				}
				else if (match.Groups[2].Success) // Single word
				{
					args.Add(match.Groups[2].Value);
					oscFormat += "s";
				}
			}

			// Append format to the OSC address pattern
			return (addressPattern, args);
		}


		private string NormalizeQuotes(string text)
		{
			return text.Replace('‘', '\'')  // Replace curly single quotes with straight single quotes
					   .Replace('’', '\'')
					   .Replace('“', '"')   // Replace curly double quotes with straight double quotes
					   .Replace('”', '"');
		}

		private void SendOSCMessage(string message)
		{
			try
			{
				string ipAddress = txtIpAddress?.Text ?? throw new ArgumentNullException("IP Address is not provided");
				if (!int.TryParse(txtPort?.Text, out int port))
				{
					DisplayAlert("Error", "Invalid port number.", "OK");
					return;
				}

				// Parse the message
				var (addressPattern, args) = ParseOSCMessage(message);

				// Create the OSC message
				var oscMessage = new OscMessage(addressPattern, args.ToArray());

				// Create the OSC sender
				using (var udpClient = new UdpClient())
				{
					var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
					var data = oscMessage.GetBytes();
					udpClient.Send(data, data.Length, endPoint);
				}

				// DisplayAlert("Success", "OSC message sent successfully.", "OK");
			}
			catch (Exception ex)
			{
				DisplayAlert("Error", "Failed to send OSC message: " + ex.Message, "OK");
			}
		}


		private void RemoveRow(Grid row)
		{
			MessageStack.Children.Remove(row);

			// Reassign row numbers after removal
			rowCount = 0;
			foreach (var child in MessageStack.Children)
			{
				if (child is Grid grid)
				{
					var rowNumberLabel = (Label)grid.Children[0];
					rowCount++;
					rowNumberLabel.Text = rowCount.ToString();
				}
			}
		}

		public class CueData
		{
			public int Id { get; set; }
			public string Title { get; set; } = string.Empty;
			public string Message { get; set; } = string.Empty;
		}




		private async void SaveButton_Clicked(object sender, EventArgs e)
		{
			try
			{
				var cues = new List<CueData>();
				var ipAddress = txtIpAddress?.Text ?? string.Empty;
				var port = txtPort?.Text ?? string.Empty;

				int id = 1; // Start numbering from 1

				foreach (var child in MessageStack.Children)
				{
					if (child is Grid grid)
					{
						if (grid.Children.Count >= 2 &&
							grid.Children[2] is Entry textBox &&
							!string.IsNullOrWhiteSpace(textBox.Text))
						{
							// Assuming the first child is a label or non-entry control for row number
							var titleLabel = grid.Children[1] as Entry;
							cues.Add(new CueData
							{
								Id = id++,
								Title = titleLabel?.Text ?? string.Empty,
								Message = textBox.Text
							});
						}
					}
				}

				if (cues.Count == 0)
				{
					await DisplayAlert("Warning", "No rows with non-empty cue strings to save.", "OK");
					return;
				}

				var saveData = new
				{
					IPAddress = ipAddress,
					Port = port,
					Cues = cues
				};

				var json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
				var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "bfg_oscsender_cues.json");

				if (string.IsNullOrWhiteSpace(filePath) || !Directory.Exists(Path.GetDirectoryName(filePath)))
				{
					await DisplayAlert("Error", "The directory for saving the file could not be found.", "OK");
					return;
				}

				File.WriteAllText(filePath, json);
				await DisplayAlert("Saved", $"Cues saved to user documents folder as \"bfg_oscsender_cues.json\"", "OK");
			}
			catch (UnauthorizedAccessException)
			{
				await DisplayAlert("Error", "Access to the file path is denied. Please check your permissions.", "OK");
			}
			catch (DirectoryNotFoundException)
			{
				await DisplayAlert("Error", "The directory for saving the file could not be found.", "OK");
			}
			catch (IOException)
			{
				await DisplayAlert("Error", "An error occurred while saving the file. Please try again.", "OK");
			}
			catch (JsonException)
			{
				await DisplayAlert("Error", "An error occurred while serializing the data. Please try again.", "OK");
			}
			catch (Exception ex)
			{
				await DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
			}
		}

		private async void LoadButton_Clicked(object sender, EventArgs e)
		{
			try
			{
				var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "bfg_oscsender_cues.json");

				if (File.Exists(filePath))
				{
					var json = File.ReadAllText(filePath);
					var saveData = JsonConvert.DeserializeObject<dynamic>(json);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
					var ipAddress = (string)saveData.IPAddress ?? string.Empty;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
					var port = (string)saveData.Port ?? string.Empty;
					var cues = JsonConvert.DeserializeObject<List<CueData>>(saveData.Cues.ToString());

					txtIpAddress.Text = ipAddress;
					txtPort.Text = port;

					MessageStack.Children.Clear();

					foreach (var cue in cues)
					{
						AddNewRow(cue.Message, cue.Title, cue.Id);
					}

					await DisplayAlert("Loaded", "Cues loaded from file.", "OK");
				}
				else
				{
					await DisplayAlert("Error", "No saved cues found. Check in user documents folder for \"bfg_oscsender_cues.json\"", "OK");
				}
			}
			catch (JsonException)
			{
				await DisplayAlert("Error", "An error occurred while deserializing the data. Please try again.", "OK");
			}
			catch (Exception ex)
			{
				await DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
			}
		}



	}
}