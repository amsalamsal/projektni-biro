# Registar 3D modela, tekstura i licenci

Stanje: verzija 0.2.0.

## Vanjski sadržaj

**Nije preuzet ni uključen nijedan vanjski 3D model, tekstura, font ni ikona.** Paketi ne sadrže materijal treće
strane za koji bi trebala licenca.

| Vrsta | Izvor | Licenca | Napomena |
|---|---|---|---|
| 3D modeli elemenata | generišu se parametarski u kodu (`core/builders/*.rb`) | dio projekta | šematski ili standardni prikaz, nije proizvod proizvođača |
| 2D simboli (AutoCAD) | generišu se u kodu (`SiteDesignPro.Core/Symbols`) | dio projekta | – |
| Materijali | boje i parametri u `shared/catalog/materials.json` | dio projekta | bez tekstura (slike) |
| Ikone | `shared/icons/*.svg`, nacrtane za projekat | dio projekta | rasterizovane alatom `tools/icons` |

## Pravila za buduće modele (faza realističnih modela)

Realistični modeli (stabla, mobilijar, rasvjeta proizvođača…) uvodit će se preko **dodjele komponenti** u SketchUpu
(*Biblioteka i postavke... → vrsti dodijeliti .skp*). Već danas korisnik može vrsti dodijeliti vlastiti `.skp`.

Prije uključivanja bilo kojeg modela u paket za svaki model se upisuje:

| Polje | Primjer |
|---|---|
| Datoteka | `assets/trees/acer_platanoides_8m.skp` |
| Izvor (URL ili proizvođač) | – |
| Autor | – |
| Licenca i verzija | npr. CC BY 4.0, CC0, licenca proizvođača |
| Dozvoljena redistribucija | da / ne |
| Obavezno navođenje autora | tekst navođenja |
| Datum preuzimanja i SHA-256 | – |

Modeli s licencom koja ne dozvoljava redistribuciju (npr. većina modela iz 3D Warehousea) **ne smiju** se uključiti u
paket. Korisnik ih može samo sam dodijeliti u svom modelu.
