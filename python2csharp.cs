namespace Namespace {
    
    using os;
    
    using signal;
    
    using @struct;
    
    using datetime = datetime.datetime;
    
    using sleep = time.sleep;
    
    using serial;
    
    using Image = PIL.Image;
    
    using ImageDraw = PIL.ImageDraw;
    
    using ImageFont = PIL.ImageFont;
    
    using System.Diagnostics;
    
    using System;
    
    using System.Linq;
    
    using System.Collections.Generic;
    
    public static class Module {
        
        public static object COM_PORT = "/dev/ttyACM0";
        
        public static object DISPLAY_WIDTH = 320;
        
        public static object DISPLAY_HEIGHT = 480;
        
        public class Command {
            
            public object RESET = 101;
            
            public object CLEAR = 102;
            
            public object SCREEN_OFF = 108;
            
            public object SCREEN_ON = 109;
            
            public object SET_BRIGHTNESS = 110;
            
            public object DISPLAY_BITMAP = 197;
        }
        
        public static object SendReg(
            object ser = serial.Serial,
            object cmd = @int,
            object x = @int,
            object y = @int,
            object ex = @int,
            object ey = @int) {
            var byteBuffer = bytearray(6);
            byteBuffer[0] = x >> 2;
            byteBuffer[1] = ((x & 3) << 6) + (y >> 4);
            byteBuffer[2] = ((y & 15) << 4) + (ex >> 6);
            byteBuffer[3] = ((ex & 63) << 2) + (ey >> 8);
            byteBuffer[4] = ey & 255;
            byteBuffer[5] = cmd;
            ser.write(bytes(byteBuffer));
        }
        
        public static object Reset(object ser = serial.Serial) {
            SendReg(ser, Command.RESET, 0, 0, 0, 0);
        }
        
        public static object Clear(object ser = serial.Serial) {
            SendReg(ser, Command.CLEAR, 0, 0, 0, 0);
        }
        
        public static object ScreenOff(object ser = serial.Serial) {
            SendReg(ser, Command.SCREEN_OFF, 0, 0, 0, 0);
        }
        
        public static object ScreenOn(object ser = serial.Serial) {
            SendReg(ser, Command.SCREEN_ON, 0, 0, 0, 0);
        }
        
        public static object SetBrightness(object ser = serial.Serial, object level = @int) {
            // Level : 0 (brightest) - 255 (darkest)
            Debug.Assert(255 >= level >= 0);
            Debug.Assert("Brightness level must be [0-255]");
            SendReg(ser, Command.SET_BRIGHTNESS, level, 0, 0, 0);
        }
        
        public static object DisplayPILImage(object ser = serial.Serial, object image = Image, object x = @int, object y = @int) {
            var image_height = image.size[1];
            var image_width = image.size[0];
            Debug.Assert(image_height > 0);
            Debug.Assert("Image width must be > 0");
            Debug.Assert(image_width > 0);
            Debug.Assert("Image height must be > 0");
            SendReg(ser, Command.DISPLAY_BITMAP, x, y, x + image_width - 1, y + image_height - 1);
            var pix = image.load();
            var line = bytes();
            foreach (var h in Enumerable.Range(0, image_height)) {
                foreach (var w in Enumerable.Range(0, image_width)) {
                    var R = pix[w,h][0] >> 3;
                    var G = pix[w,h][1] >> 2;
                    var B = pix[w,h][2] >> 3;
                    var rgb = R << 11 | G << 5 | B;
                    line += @struct.pack("H", rgb);
                    // Send image data by multiple of DISPLAY_WIDTH bytes
                    if (line.Count >= DISPLAY_WIDTH * 8) {
                        ser.write(line);
                        line = bytes();
                    }
                }
            }
            // Write last line if needed
            if (line.Count > 0) {
                ser.write(line);
            }
            sleep(0.01);
        }
        
        public static object DisplayBitmap(object ser = serial.Serial, object bitmap_path = str, object x = 0, object y = 0) {
            var image = Image.open(bitmap_path);
            DisplayPILImage(ser, image, x, y);
        }
        
        public static object DisplayText(
            object ser = serial.Serial,
            object text = str,
            object x = 0,
            object y = 0,
            object font = "roboto/Roboto-Regular.ttf",
            object font_size = 20,
            object font_color = (0, 0, 0),
            object background_color = (255, 255, 255),
            object background_image = null) {
            object text_image;
            // Convert text to bitmap using PIL and display it
            // Provide the background image path to display text with transparent background
            Debug.Assert(text.Count > 0);
            Debug.Assert("Text must not be empty");
            Debug.Assert(font_size > 0);
            Debug.Assert("Font size must be > 0");
            if (background_image == null) {
                // A text bitmap is created with max width/height by default : text with solid background
                text_image = Image.@new("RGB", (DISPLAY_WIDTH, DISPLAY_HEIGHT), background_color);
            } else {
                // The text bitmap is created from provided background image : text with transparent background
                text_image = Image.open(background_image);
            }
            // Draw text with specified color & font
            font = ImageFont.truetype("./res/fonts/" + font, font_size);
            var d = ImageDraw.Draw(text_image);
            d.text((x, y), text, font: font, fill: font_color);
            // Crop text bitmap to keep only the text
            var _tup_1 = d.textsize(text, font: font);
            var text_width = _tup_1.Item1;
            var text_height = _tup_1.Item2;
            text_image = text_image.crop(box: (x, y, min(x + text_width, DISPLAY_WIDTH), min(y + text_height, DISPLAY_HEIGHT)));
            DisplayPILImage(ser, text_image, x, y);
        }
        
        public static object DisplayProgressBar(
            object ser = serial.Serial,
            object x = @int,
            object y = @int,
            object width = @int,
            object height = @int,
            object min_value = 0,
            object max_value = 100,
            object value = 50,
            object bar_color = (0, 0, 0),
            object bar_outline = true,
            object background_color = (255, 255, 255),
            object background_image = null) {
            // Generate a progress bar and display it
            // Provide the background image path to display progress bar with transparent background
            Debug.Assert(x + width <= DISPLAY_WIDTH);
            Debug.Assert("Progress bar width exceeds display width");
            Debug.Assert(y + height <= DISPLAY_HEIGHT);
            Debug.Assert("Progress bar height exceeds display height");
            Debug.Assert(min_value <= value <= max_value);
            Debug.Assert("Progress bar value shall be between min and max");
            if (background_image == null) {
                // A bitmap is created with solid background
                var bar_image = Image.@new("RGB", (width, height), background_color);
            } else {
                // A bitmap is created from provided background image
                bar_image = Image.open(background_image);
                // Crop bitmap to keep only the progress bar background
                bar_image = bar_image.crop(box: (x, y, x + width, y + height));
            }
            // Draw progress bar
            var bar_filled_width = value / (max_value - min_value) * width;
            var draw = ImageDraw.Draw(bar_image);
            draw.rectangle(new List<object> {
                0,
                0,
                bar_filled_width - 1,
                height - 1
            }, fill: bar_color, outline: bar_color);
            if (bar_outline) {
                // Draw outline
                draw.rectangle(new List<object> {
                    0,
                    0,
                    width - 1,
                    height - 1
                }, fill: null, outline: bar_color);
            }
            DisplayPILImage(ser, bar_image, x, y);
        }
        
        public static object stop = false;
        
        public static object sighandler(object signum, object frame) {
            stop = true;
        }
        
        static Module() {
            signal.signal(signal.SIGINT, sighandler);
            signal.signal(signal.SIGTERM, sighandler);
            signal.signal(signal.SIGQUIT, sighandler);
            Clear(lcd_comm);
            SetBrightness(lcd_comm, 0);
            DisplayBitmap(lcd_comm, "res/example.png");
            DisplayText(lcd_comm, "Basic text", 50, 100);
            DisplayText(lcd_comm, "Custom italic text", 5, 150, font: "roboto/Roboto-Italic.ttf", font_size: 30, font_color: (0, 0, 255), background_color: (255, 255, 0));
            DisplayText(lcd_comm, "Transparent bold text", 5, 300, font: "geforce/GeForce-Bold.ttf", font_size: 30, font_color: (255, 255, 255), background_image: "res/example.png");
            DisplayText(lcd_comm, "Text overflow!", 5, 430, font: "roboto/Roboto-Bold.ttf", font_size: 60, font_color: (255, 255, 255), background_image: "res/example.png");
            DisplayText(lcd_comm, datetime.now().time().ToString(), 160, 2, font: "roboto/Roboto-Bold.ttf", font_size: 20, font_color: (255, 0, 0), background_image: "res/example.png");
            DisplayProgressBar(lcd_comm, 10, 40, width: 140, height: 30, min_value: 0, max_value: 100, value: bar_value, bar_color: (255, 255, 0), bar_outline: true, background_image: "res/example.png");
            DisplayProgressBar(lcd_comm, 160, 40, width: 140, height: 30, min_value: 0, max_value: 19, value: bar_value % 20, bar_color: (0, 255, 0), bar_outline: false, background_image: "res/example.png");
            lcd_comm.close();
        }
        
        public static object is_posix = os.name == "posix";
        
        public static object lcd_comm = serial.Serial(COM_PORT, 115200, timeout: 1, rtscts: 1);
        
        public static object bar_value = 0;
        
        public static object bar_value = (bar_value + 2) % 101;
    }
}
