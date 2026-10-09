using System.Collections.ObjectModel;

namespace VideoGrabber.Core.ClientUpdates;

public static class ClientReleaseTrust
{
    public const string KeyId = "vg-client-20261009";
    public static IReadOnlyDictionary<string, string> PublicKeys { get; }
        = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [KeyId] = """
                -----BEGIN PUBLIC KEY-----
                MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAwCPIpMDMv8zfQIj0REqm
                X0Z/TeANmrd6ECNgIZ33JtgZ1TcG05qt9/GDpd2gXwCZxvGXHGZdIAnAjbiwTrG1
                uH8gIHRnJwjrYWOW33l350qCuhZ4/3HMYlBf5466NKnv/MNClgZtj0Wfcs8mEuQ9
                qc3MaWohlQ55YJakabv6pOHtuckJaUAFVqpBYDfkYgC1NRFtYjKo1jUsZImPJsxh
                RaHxB7Aa6bMTWmx+03Q5+Ws9SCAbnW5yly3gJYg+vYBzYQplFtQarDfXUN/QV19M
                WEjupfkmDzjOy43Bx6nV/bjv3jsqIXuoVT4uTV0SPaZw2AMV2jry9DGUYpdM8+Gj
                gyhseda9Sf0X90x7DGvHG2glXsaZTjHgwaZbKTfKGixtryetg0nVai8rWr+IUrFQ
                jaEtUppg0FSjUgXOEeJDKovDKlqOklN3zq+xtJ6p9EYThODfRUD0Op5d1uulx/q+
                rOhN9S4TtKrGNOUGzHSpibXG+QJ0cs9249QOSRcjkb/pAgMBAAE=
                -----END PUBLIC KEY-----
                """
        });
}
