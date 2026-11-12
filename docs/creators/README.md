# Dokumentacja dla twórców gier

Jak opublikować grę w SGS i jak podłączyć ją do platformy.

| Strona | Temat |
|---|---|
| [Jak opublikować grę](how-to-publish.md) | Konto, folder, `sgs-manifest.json`, Panel twórcy |
| [Grafika sklepowa](store-art.md) | Miniaturka, okładka pozioma, logo, zrzuty ekranu |
| [Osiągnięcia i statystyki](achievements.md) | Tabela osiągnięć i statystyk w stylu Steamworks w Panelu twórcy, odblokowywanie i postęp z dowolnego silnika przez HTTP |
| [Lobby i zaproszenia](lobbies.md) | Znajdowanie hosta po kodzie, zaproszenia, dołączanie z klienta |

Schemat i przykład paczki:

- [`schemas/sgs-manifest.v1.json`](https://github.com/M455YN/SGSClient/blob/master/schemas/sgs-manifest.v1.json)
- [`schemas/examples/demo-runner.sgs-manifest.json`](https://github.com/M455YN/SGSClient/blob/master/schemas/examples/demo-runner.sgs-manifest.json)
- **Gotowy szablon do rozpakowania:** [`schemas/examples/sample-game-package.zip`](https://github.com/M455YN/SGSClient/blob/master/schemas/examples/sample-game-package.zip) (folder: [`sample-game/`](https://github.com/M455YN/SGSClient/tree/master/schemas/examples/sample-game))
- **Szablony gier z bazy (exe i wersja):** [`schemas/examples/catalog-templates/`](https://github.com/M455YN/SGSClient/tree/master/schemas/examples/catalog-templates)

Informacje o integracji technicznej znajdziesz w [Integracji z SGS](../developers/README.md).
