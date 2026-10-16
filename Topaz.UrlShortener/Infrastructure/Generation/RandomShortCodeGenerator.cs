using System.Security.Cryptography;
using Topaz.UrlShortener.Application.Interfaces;

namespace Topaz.UrlShortener.Infrastructure.Generation;

public sealed class RandomShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int CodeLength = 8;

    public string Generate()
    {
        var characters = new char[CodeLength];

        for (var index = 0; index < characters.Length; index++)
        {
            var position = RandomNumberGenerator.GetInt32(Alphabet.Length);

            characters[index] = Alphabet[position];
        }

        return new string(characters);
    }
}