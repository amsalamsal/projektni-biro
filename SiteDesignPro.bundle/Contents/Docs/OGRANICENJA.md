# Poznata ograničenja

## Nije provjereno u host aplikacijama

Razvojno okruženje nije imalo Windows, AutoCAD ni SketchUp. Zato:

* **Dodatak za AutoCAD se kompajlira za sve četiri verzije, ali nikada nije učitan u AutoCAD.** Traka, paleta,
  dijalozi (WinForms, WPF), pregledi (jig), šrafure, blokovi, tabele, multileaderi, XData kota i sigurnosne kopije
  DWG-a nisu provjereni u radu.
* **Proširenje za SketchUp radilo je samo protiv lažnog (fake) SketchUp API-ja u repozitoriju.** Stvarno ponašanje
  (orijentacija lica, rupe, mekane ivice, `HtmlDialog`, undo, `save_copy`, brzina na velikim modelima) nije
  provjereno.
* HTML dijalozi su testirani u Chromiumu, a ne u SketchUpovom CEF-u.
* Paketi `.bundle` i `.rbz` su napravljeni, ali nisu instalirani.
* Nema stvarnog DWG ni SKP testnog crteža. Ulazni tok rada testiran je na sintetičkom primjeru izvornog crteža.

## Kote, teren i georeferenciranje

* Kota se dodjeljuje elementima projekta i, preko XData, običnim objektima crteža. Mijenjaju se `Elevation` (LW i
  2D polilinije, šrafure) ili Z (blokovi, tačke, linije, krugovi, lukovi).
  * **3D polilinija se izravnava na zadanu kotu i gubi nagib**; `SDPKOTA` na to upozorava prije potvrde.
  * Ostali tipovi objekata (npr. splajn, mreža, solid) ne mogu dobiti kotu i naredba to javlja.
* Oznake kota se čitaju iz TEXT, MTEXT, multileadera i atributa blokova. Kote iz dimenzija se ne čitaju.
* Teren je TIN bez zaglađivanja. Izohipse se dijele na tačke prema postavkama terena.
  * Nema automatskog modeliranja usjeka i nasipa oko objekata, ni projektovanog (novog) terena.
  * Analiza odvodnje prijavljuje samo depresije i nagibe; to nije hidraulički proračun.
* Transformacija MGI 1901 → WGS84 koristi približne Helmertove parametre (greška nekoliko metara). KML je samo za
  orijentaciju. Koordinate u crtežu se nikad ne mijenjaju na osnovu KML-a.
* Podržani koordinatni sistemi: EPSG:3907–3909, 31275–31277, 25833/25834, 32633/32634. Ostali se mogu upisati, ali
  se za njih ne računaju geografske koordinate.

## Funkcionalna ograničenja

* SketchUp → AutoCAD prenosi izmjene parametara te pomjeranje i rotaciju **tačkastih** elemenata. Linije i
  površine se mijenjaju u AutoCAD-u.
* Geometrija mora biti u ravni XY svijeta. Podržani su linije, lukovi, krugovi, lake (LW) i 3D polilinije. Stare 2D
  polilinije, splajnove i elipse prvo pretvorite u LW poliliniju.
* 3D modeli su **šematski parametarski prikazi**, a ne proizvodi proizvođača. Realistični modeli su planirani preko
  dodjele komponenti (pogledajte [registar licenci](REGISTAR_LICENCI.md)).
* Provjere su pomoć pri projektovanju, a **ne provjera usklađenosti s propisima**. Vrijednosti označene za reviziju
  mora pregledati odgovorni stručnjak. Krugovi pokrivenosti rasvjete su ilustrativni.
* Slojevi verzije 0.1 se ne preimenuju automatski (`SDPLAYERS` → *Stari*). Oznake `SDP …` u postojećim SketchUp
  modelima ostaju.
* Jedan projekat po crtežu. SketchUp model može imati više projekata.
* Sigurnosna kopija u AutoCAD-u kopira **zadnju sačuvanu** verziju crteža. Nesačuvane izmjene nisu u kopiji.
  Nedovršena operacija prekinuta padom AutoCAD-a ne može se nastaviti; kopija se otvara kao novi crtež.
* Razvojna dokumentacija (arhitektura, build, biblioteka objekata) je ostala na engleskom jeziku.

## Performanse

Mjerene su samo za dijelove nezavisne od host aplikacija:

* `sdp stress`;
* Ruby graditelji;
* triangulacija s nekoliko hiljada tačaka u testovima.

Vrijeme uvoza hiljada elemenata u stvarnom SketchUpu i vrijeme triangulacije velikih geodetskih snimaka u
AutoCAD-u nisu poznati.
