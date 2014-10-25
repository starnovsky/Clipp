using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Perficient.CloudClippboard.Entities.Common
{
    public class UniqueKeyGenerator
    {
        //string vowels = "aeiou";
        //string consonants = "bcdfghjklmnprstvwxyz";

        /*
           The reason for the duplicate letters is to add "weighting" to certain letters to allow them more chance
           of being randomly selected.  This is due to the fact that certain letters in the English language are more
           frequently used than others.

           The breakdown of usage is as follows (from most frequent to least frequent):
            1.  E                   (7)
            2.  T                   (6)
            3.  A, O, N, R, I, S    (5)
            4.  H                   (4)
            5.  D, L, F, C, M, U    (3)
            6.  G, Y, P, W, B       (2)
            7.  V, K, X, J, Q, Z    (1)
        */

        private readonly string Vowels = "aaaaaeeeeeeeiiiiiooooouuu";
        private readonly string Consonants = "bbcccdddfffgghhhhjklllmmmnnnnnpprrrrrsssssttttttvwwxyyz";

        private readonly string[] Vowelafter = { "th", "ch", "sh", "qu" };
        private readonly string[] Consonantafter = { "oo", "ee" };

        private readonly Random _rnd = new Random();
       

        public string GenerateKey(int length = 5)
        {
            string pass = "";
            bool isvowel = false;

            for (int i = 0; i < length; i++)
            {
                if (isvowel)
                {
                    if (_rnd.Next(0, 5) == 0 && i < (length - 1))
                    {
                        pass += Consonantafter[_rnd.Next(0, Consonantafter.Length)];
                    }
                    else
                    {
                        pass += Vowels.Substring(_rnd.Next(0, Vowels.Length), 1);
                    }
                }
                else
                {
                    if (_rnd.Next(0, 5) == 0 && i < (length - 1))
                    {
                        pass += Vowelafter[_rnd.Next(0, Vowelafter.Length)];
                    }
                    else
                    {
                        pass += Consonants.Substring(_rnd.Next(0, Consonants.Length), 1);
                    }
                }
                isvowel = !isvowel;
            }
            return pass;
        }
    }
}
