using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNovUtilsST
{
    public class SchemespecViewModel : INotifyPropertyChanged
    {

        private string _output1 = "Ростверки кустовые"; public string output1 { get => _output1; set { _output1 = value; OnPropertyChanged(); } }
        private string _output2 = "Ростверки ленточные"; public string output2 { get => _output2; set { _output2 = value; OnPropertyChanged(); } }
        private string _output3 = "Приямки"; public string output3 { get => _output3; set { _output3 = value; OnPropertyChanged(); } }
        private string _output4 = "Приямки под лифты"; public string output4 { get => _output4; set { _output4 = value; OnPropertyChanged(); } }
        private string _output5 = "Фундаментная плита"; public string output5 { get => _output5; set { _output5 = value; OnPropertyChanged(); } }
        private string _output6 = "Бетонные полы"; public string output6 { get => _output6; set { _output6 = value; OnPropertyChanged(); } }
        private string _output7 = "Стены монолитные"; public string output7 { get => _output7; set { _output7 = value; OnPropertyChanged(); } }
        private string _output8 = "Лестничная клетка"; public string output8 { get => _output8; set { _output8 = value; OnPropertyChanged(); } }
        private string _output9 = "Лестницы монолитные"; public string output9 { get => _output9; set { _output9 = value; OnPropertyChanged(); } }
        private string _output10 = "Лестничные площадки монолитные"; public string output10 { get => _output10; set { _output10 = value; OnPropertyChanged(); } }
        private string _output11 = "Диафрагмы жесткости"; public string output11 { get => _output11; set { _output11 = value; OnPropertyChanged(); } }
        private string _output12 = "Колонны"; public string output12 { get => _output12; set { _output12 = value; OnPropertyChanged(); } }
        private string _output13 = "Пилоны"; public string output13 { get => _output13; set { _output13 = value; OnPropertyChanged(); } }
        private string _output14 = "Плиты"; public string output14 { get => _output14; set { _output14 = value; OnPropertyChanged(); } }
        private string _output15 = "Балки монолитные"; public string output15 { get => _output15; set { _output15 = value; OnPropertyChanged(); } }
        private string _output16 = "Парапеты"; public string output16 { get => _output16; set { _output16 = value; OnPropertyChanged(); } }
        private string _output17 = "Декоративные стены"; public string output17 { get => _output17; set { _output17 = value; OnPropertyChanged(); } }
        private string _output18 = "Канал монолитный"; public string output18 { get => _output18; set { _output18 = value; OnPropertyChanged(); } }
        private string _output19 = "Выпуски из фундамента"; public string output19 { get => _output19; set { _output19 = value; OnPropertyChanged(); } }


        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

    }
}
