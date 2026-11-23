# Izmjene (Changelog)

## Neobjavljeno

### Verzija 0.3.1 – prozori svuda (20.11.)
* **Nulta kota** (SDPDATUM) i **georeferenciranje** (SDPGEOREF) u prozorima: kontrolne tačke s odstupanjima,
  Dodaj / Izračunaj / Obriši / Poništi kao dugmad, rezultat izračuna s dugmetom *Primijeni*.
* Teren, profil, analiza terena, kote iz oznaka, pokazatelji, prohodnost, tabele, količine, provjere crteža, KML,
  zone sigurnosti, rasvjeta, garažna rampa: **rezultat u prozoru** na kraju naredbe (ne treba čitati komandnu liniju).
* Tehnički minimum (npr. prestrma rampa) javlja se u prozoru istog izgleda kao ostali.
* Paleta *Prvi koraci* usklađena s ribbonom (Saobraćajnice, Sinhronizacija sa SketchUpom).
* Brže na velikim crtežima: automatsko ponovno crtanje saobraćajnica se preskače kad se ništa od čega se crtaju
  (osovine, parametri, kote, prijelazi, rampe) nije promijenilo.
* SketchUp: rezultati i greške saobraćajnica, slanja u AutoCAD i *O programu* u prozoru istog izgleda kao ostali
  dijalozi (može se kopirati); prozor Sinhronizacija u istom stilu.
* **DEINSTALIRAJ_SVE.bat** uklanja AutoCAD i SketchUp dodatak odjednom.
* Test uživo (AutoCAD 2021 + SketchUp 2026), ispravljeno:
  * ulice nacrtane u AutoCAD-u nisu dobijale 3D u SketchUpu (preuzimale su se samo oznake i prijelazi);
  * dugme *Dodaj tačku* u georeferenciranju je bilo odsječeno;
  * izvoz nesačuvanog crteža je nudio folder AutoCAD predloška i naziv „acadiso”; sada Dokumenti i naziv projekta;
  * poslije *Pošalji u SketchUp* prozor s rezultatom; tabele (količine) u prozoru kao redovi;
  * prozori SketchUpa se otvaraju iznad SketchUpa i na drugom monitoru.
* SketchUp: kod ponovnog preuzimanja iz AutoCAD-a u isti model (Sinhronizacija) ponovno građenje saobraćajnica je brisalo
  sve ostale elemente tog projekta (drveće, mobilijar…). Sada briše samo elemente saobraćajnica.
* **Brzi početak sa slikama** (Pomoć → Brzi početak) – kraći, sa snimcima ekrana.

### Verzija 0.3.0 – lakše korištenje (20.11.)
* **AutoCAD ribbon** složen po redoslijedu rada, bez duplikata: Projekat, Elementi, Saobraćajnice, Parking,
  Izmjena, Dokumentacija, Provjera, SketchUp, Pomoć (rijetki alati u Napredno). Glavni alat svakog panela je veliko
  dugme, ostalo po tri u koloni.
* **Sinhronizacija sa SketchUpom** jednim dugmetom u oba programa: zadnje poslano i preuzeto, verzija, *Pošalji* i
  *Preuzmi*. Poslije preuzimanja u AutoCAD-u sažetak u prozoru (novi, zamijenjeni, preskočeni, obrisani); u
  SketchUpu rezultat slanja u prozoru koji se može kopirati.
* SketchUp: ulice i ceste iz AutoCAD-a se pri preuzimanju **odmah** prebacuju u alate Saobraćajnice (bez posebne
  naredbe); upozorenje kad je oblik površine ručno mijenjan (u AutoCAD idu pomjeranje i parametri); jedan meni
  (Sinhronizacija, Preuzmi, Pošalji, Svojstva, Biblioteka, Saobraćajnice, Scene, Napredno).
* Upozorenje kad AutoCAD i SketchUp dodatak nisu iste verzije.
* **Desni klik** na element: Uredi element, Uredi ulicu, Oznaka.
* **Nova greška se javlja odmah** poslije naredbe koja crta ili mijenja elemente (kolizija, izlazak iz parcele …).
* Prozori s pitanjima imaju dugme **Zadane vrijednosti**.
* **Primjeri alata** (Projekat → Primjer projekta): ulica s raskrsnicom, kružni tok, garažna rampa, parking
  okomito/paralelno/45°, pješački prijelaz, drvored.
* **Brzi početak** (Pomoć): prvi projekat za 15 minuta.
* **INSTALIRAJ_SVE.bat**: AutoCAD i SketchUp dodatak jednim klikom.
* Ujednačeni nazivi: *Pošalji u SketchUp / Preuzmi iz SketchUpa* (AutoCAD) i *Pošalji u AutoCAD / Preuzmi iz
  AutoCAD-a* (SketchUp).

### Ispravka: uvoz iz SketchUpa javljao „obrisane“ elemente koji nisu obrisani (20.11.)
* AutoCAD je smatrao obrisanim svaki saobraćajni element kojeg nije bilo u paketu (npr. ulice iz AutoCAD-a koje
  nisu preuzete u alate Saobraćajnice, ili kote koje su u SketchUpu postale visina osovine). Sada SketchUp u paketu
  šalje spisak onoga što je stvarno obrisano (od elemenata koje je dobio iz AutoCAD-a ili poslao u AutoCAD), i
  AutoCAD nudi brisanje samo njih. Kote koje su ušle u visinu osovine vraćaju se u AutoCAD pod svojim id-om.

### Izvoz iz SketchUpa za sve elemente; prozori za datoteke (20.11.)
* SketchUp: **Izvoz za AutoCAD (.sdpkg)…** (glavni meni i Saobraćajnice) šalje u jednom paketu saobraćajnice i
  **sve ostale elemente iz biblioteke**: postavljene u SketchUpu i one iz AutoCAD projekta – gdje su sada
  (pomjereni, zarotirani) i s parametrima izmijenjenim u SketchUpu. U AutoCAD-u Uvoz paketa dodaje nove i zamjenjuje
  postojeće (isti id).
* AutoCAD: odabir datoteke (uvoz paketa, izvoz, izmjene, CSV, KML) je uvijek Windows prozor za odabir datoteke,
  i kad je FILEDIA = 0; pamti zadnji folder.

### Unos u prozorima umjesto komandne linije (20.11.)
* Pitanja naredbi su u prozorima, sva pitanja jedne naredbe u jednom prozoru, s pamćenjem zadnjih unosa i
  provjerom prije „U redu“ (npr. nagib rampe, širina trake). Ulica i cesta (osovina i visine), kružni tok, pješački
  prijelaz, visine saobraćajnica, garažna rampa, duž linije, popuna površine, niz parking mjesta, parking u nizu,
  izmjena elementa (SDPEDIT – svi parametri odjednom), postavke projekta, dokumentovanje, razmjernik, zaglavlje,
  detalj, prohodnost, sigurnosne kopije. Ostala pojedinačna pitanja su u malom prozoru.
* Prozori se otvaraju i kad je FILEDIA = 0 (ranije je to prebacivalo sve na komandnu liniju); skripte i AutoLISP
  i dalje pitaju u komandnoj liniji. `SDPPROZORI` prebacuje na komandnu liniju i nazad.

### Inputi 19.11.: rampe i parking mjesta u nizu (20.11.)
* **Garažna rampa**: prvi i zadnji metar su ravni (bez poprečnog pada), u ravni s ulicom ili pločom; poprečni pad
  3 % počinje nakon 1 m i raste na punu vrijednost na 2 m (AutoCAD 3D i SketchUp, isti motor).
* **Rampe ispod tehničkog minimuma se ne mogu projektovati**: garažne (nagib ≤ 17 %, unutrašnji poluprečnik
  ≥ 5,00 m, traka ≥ 2,50 / 3,50 m, zaštitni razmak, svijetla visina ≥ 2,10 m) i pješačke (≤ 8,33 %, ≥ 1,20 m).
  AutoCAD odbija element s porukom, SketchUp poništava izmjenu; provjera javlja postojeće kao `SDP-C012`.
* **AutoCAD – Parking mjesta u nizu** (nova kartica *Parking mjesta*): okomito (s mjestima za osobe s
  invaliditetom ispred broja 1: šrafura 1,40 m i rampa za trotoar), paralelno i pod uglom 45°; numeracija se
  nastavlja ili kreće od 1; strelice u prolazu, ugao 45°, bez kota. Oznake idu i u SketchUp 3D.

### Saobraćajnice u oba smjera: SketchUp ⇄ AutoCAD (20.11.)
* **SketchUp → AutoCAD:** nova stavka *Izvoz saobraćajnica za AutoCAD…* (motor `sdp-ceste export`) snima izvorne
  elemente alata Saobraćajnice u `.sdpkg`; visine osovina postaju kote. Generisani elementi su u paketu označeni
  (`derivedFromRoads`).
* **AutoCAD `SDPUVOZPAKETA`:** preskače generisane dijelove (kolovoz, trotoari, ivičnjaci, oznake, površine rampi),
  dodaje ili zamjenjuje izvorne elemente i odmah ponovo crta mrežu ulica. Paket iz SketchUpa za isti projekat nudi
  brisanje ulica kojih u njemu više nema.
* **AutoCAD → SketchUp:** *Preuzmi saobraćajnice iz AutoCAD-a* pretvara ulice uvezenog projekta u osovine; projekat
  ostaje povezan, pa naredni uvozi iz AutoCAD-a mijenjaju osovine (nove, izmijenjene, obrisane), a ne 3D. Kote na
  tački osovine se vraćaju u visinu osovine.
* Koordinate preko ishodišta AutoCAD projekta (korijenska grupa uvoza). Testovi: C# (izvoz motora) i Ruby (cijeli
  krug s pravim motorom).

### Saobraćajnice u SketchUpu bez AutoCAD-a (18.11.)
* **Nova traka i meni *Saobraćajnice*** u SketchUp proširenju: Ulica, Uredi ulicu, Kružni tok, Pješački prijelaz,
  Cesta (kolni prilaz, pristupna, servisna), Kota saobraćajnice, Rampa za invalide, Garažna rampa (sve četiri vrste),
  Presjek rampe, Visine i postavke, Osvježi, Ulica iz odabranih linija – sve opcije kao u AutoCAD-u.
* **Isti motor kao u AutoCAD-u**: saobraćajnice gradi program `sdp-ceste` (ista C# biblioteka mreže ulica) koji
  dolazi uz proširenje (`engine/win` – .NET Framework, dio Windowsa; `engine/net8` – .NET 8 za macOS). Rezultat je
  paket koji se uvozi kao i svaki drugi: pri izmjeni se mijenja samo ono što se promijenilo.
* **Osovine su SketchUp grupe**: pomjeranje, rotacija, kopiranje, brisanje i pomjeranje tačaka (i po visini) osvježe
  saobraćajnice same, u istom koraku za *Poništi*. Visine tačaka osovine postaju kote ulice.
* **Kružni tok** je premješten u zajedničku biblioteku (`Roundabout`): AutoCAD i SketchUp crtaju isti.
* Testovi: motor (C#), izvorni elementi i izgradnja (Ruby, lažni SketchUp API), cijeli tok s pravim motorom u CI-ju.

### Sistematsko testiranje saobraćajnica (17.11. uveče)
* **Automatska pravila za svaku mrežu** (provjeravaju se pri svakoj izgradnji, na 160+ situacija): površine se ne
  preklapaju, između njih nema šavova, nema komadića (sliver), ništa od ulice ne leži na garažnoj rampi, svaka rampa
  uz ulicu je spojena kolnim prilazom, oznake i strelice su na kolovozu, 3D visine su konačne i u razumnom opsegu.
* **Matrica situacija**: T i X raskrsnice pod 20–160°, sa i bez biciklističkih staza; sve opcije ulice (1–3 trake,
  jednosmjerna, razdjelni pojas, bez trotoara, širok trotoar, staza s jedne strane, širine traka, radijusi 1–15 m);
  krivine R20 i R50 s raskrsnicom; garažne rampe pod 30–150° s obje strane, u trotoaru i iza njega; rampa uz
  raskrsnicu, dvije rampe jedna do druge, izlazna rampa; kružni tokovi s 3 i 5 krakova; pješački prelazi; kratka
  ulica, dupla tačka, skoro ravna osovina.
* **Biciklistička staza koja završava na uglu raskrsnice** (ulica iza ugla nema stazu na toj strani): staza se
  sužava do nule na 6 m, a trotoar i rubnjak je prate – nema više odsječenog kraja i stepenice u trotoaru. Radi i kod
  kosih uglova; uz kružni tok (zakrivljen prsten) staza i dalje završava na kraju luka.
* **Smjer staze kod kolnog prilaza** se traži u lokalnim koordinatama mreže (kod crteža u državnim koordinatama se
  mogao uzeti smjer pogrešne ulice); nova situacija u matrici: isti crtež daleko od ishodišta.
* **Ispravljeno**: kod kosih raskrsnica (30°, 60°) kolovoz i ivičnjak su se preklapali do 4 cm uz ivicu (šiljak nulte
  širine je pri prilagođavanju lukova povlačio tačke luka); kolovoz kolnog prilaza više ne ulazi na rampu; rampa koja
  počinje do 8 m iza ruba ulice (predbašta) dobija kolni prilaz do rampe (ranije samo do 1,5 m); spušteni dio
  biciklističke staze završava okomito na stazu.

### Izvoz u SketchUp: izmjenjive plohe, SketchUp 2014–2027 (17.11.)
* **Sve plohe se mogu uređivati** (Push/Pull i ostali alati): ivice se više ne omekšavaju niti zaglađuju (Soften
  Edges 0°, bez Smooth normals). Trouglovi u istoj ravni kod pojedinačno građenih tijela spajaju se u jednu plohu;
  površine ulica s visinama ostaju trouglovi, svaki zasebna ploha.
* **Mreže trouglova bez dijagonala**: kod površina s visinama ivice između trouglova u istoj ravni su skrivene (ne
  omekšane): ravni dijelovi izgledaju kao jedna ploha, a svaki trougao se i dalje može Push/Pull-ati.
* **Spuštena biciklistička staza preko prilaza** u 3D: ravni dio i obje kosine su zasebne ravne plohe; kosine se
  mjere duž staze (i kod prilaza pod uglom), pa nema izlomljenih trouglova; ivičnjak uz kosine prati istu ravan.
* **Garažna rampa pod uglom**: trotoar, ivičnjak, biciklistička staza i rubnjak više ne leže na rampi ni na njenim
  zidovima (ugao koso postavljene rampe ulazio je u trotoar).
* **Oznake** (5 mm) sežu 1 mm u podlogu, tako da im dno nije u ravni asfalta (nema treperenja).
* **Podrška za SketchUp 2014–2027**: kod radi na Ruby 2.0 (SketchUp 2014–2016), 2.2, 2.5, 2.7 i novijim; na
  SketchUp 2014–2016 dijalozi rade u WebDialog prozoru (HtmlDialog postoji tek od 2017), ikone su PNG (vektorske od
  2016), a folderi oznaka (tagova) koriste se tek od 2021. Automatska provjera na Ruby 2.0/2.2/2.5/2.7 pri svakoj izgradnji.

### Inputi 17.11.2026 16:00 – biciklistička staza preko prilaza, krakovi kružnog toka, oznake 5 mm, spojevi
* **Biciklistička staza preko kolnog prilaza / garažne rampe**: gdje prilaz presijeca trotoar i biciklističku stazu,
  staza ide preko prilaza u nivou kolovoza, označena **tamnim bordo** (kao rampa za invalide), i spušta se do njega
  2,00 m sa obje strane; ivičnjak uz taj dio spušta se s njom. U 3D je to kosa površina od +0,15 do kolovoza.
* **Krakovi kružnog toka**: iza malog razdjelnog otoka trake oba smjera se postepeno spoje, tako da 15 m od vanjske
  ivice kružnog toka više nema razdjelnog pojasa – smjerove dijeli samo jedna puna linija. Linije uz razdjelni pojas
  prate suženje, a između njih je šrafirani klin. Ulica dobija parametre „Srednje ostrvo do“ i „Dužina spajanja traka“.
* **Oznake u 3D su debele 5 mm** i leže na asfaltu (linije, strelice, zebre, šrafure, plava staza uz prijelaz, oznake
  garažne rampe, parking).
* **Razdjelni otok u 3D**: ivičnjak okolo (12 cm), unutra trava 3 cm ispod vrha ivičnjaka; zaobljeni krajevi su
  glatki kao na crtežu (mnogo segmenata).
* **Spojevi pod uglom**: kad se dvije ulice sijeku pod uglom, ugao na tupoj strani zaobljen je većim radijusom
  (ivičnjak ide glatko, bez loma); garažna rampa/prilaz pod uglom dobija veći radijus na oštroj strani (vozilo tu
  više skreće).

### Visine upisane kao m n.v. i brži uvoz u SketchUp (17.11.)
* Visina tačke ulice, kote, garažne rampe ili ploče iznad nje upisana kao apsolutna (npr. 501,00 m n.v. kad je
  osnovna visina 500,00) automatski se pretvara u visinu u odnosu na osnovnu (+1,00) i to se ispiše. Postojeći
  crteži s takvim visinama ispravljaju se sami pri sljedećem crtanju ulica ili izvozu za SketchUp.
* Uvoz u SketchUp je višestruko brži: površine ulica s visinama i garažnih rampi dodaju se u jednom koraku (a ne
  trougao po trougao), mreža tačaka unutar površina je 1 × 1 m, a linije između trouglova su omekšane.

### Inputi 16.11.2026 16:30 – prijelaz, spoj rampe, kružni tok, 3D
* **Rampa za invalide na prijelazu s biciklističkom stazom** počinje tamo gdje staza završava i ide prema zebri
  (2,00 m preko prvog dijela prijelaza). Rampe za invalide su u crtežu i u 3D **tamnosive** (tamnije od trotoara).
* **Spoj garažne rampe sa saobraćajnicom** je uvijek zaobljen automatski (zakošenje 1,00 × 1,00 m pod 45°); pitanje
  i podešavanje radijusa su uklonjeni.
* **Pomoćna linija okomita na saobraćajnicu**: kad prva tačka rampe padne uz ulicu, pri biranju sljedeće tačke vidi se
  plava pomoćna linija okomita na ulicu, a tačka se „zalijepi“ za nju kad je kursor unutar 6°.
* **Kružni tok**:
  * kad prsten ima jednu traku, sredinom prstena ide ista isprekidana linija (3 m linija, 3 m razmak) kao na kracima;
  * zelena površina je 15 cm iznad asfalta i oko nje je ivičnjak (15 cm, iscrtan u crtežu i u 3D);
  * prijelazni prsten i ivičnjak uz saobraćajnu traku su u nivou asfalta;
  * prijelazni prsten je od **granitne kaldrme** (šrafura kocke ~12 cm u crtežu, tekstura u 3D).
* **3D (SketchUp)**:
  * na ulicama s visinama sve oznake (linije, strelice, pješački prijelazi, šrafirane površine, biciklistička staza uz
    prijelaz) leže na kosoj površini ulice, 5 mm iznad nje – više nisu ispod ili iznad asfalta;
  * ivičnjaci, trotoari i staze ulica s visinama su zatvorena tijela s bočnim stranama (bok ivičnjaka je spojen s
    asfaltom);
  * garažne rampe: pod je materijal **Asphalt New**, zidovi **Blacktop Old 02** (iz biblioteke SketchUpa; ako je nema,
    slična vlastita tekstura), a u zidovima su **LED svjetla 8 × 3 cm** na 40 cm od poda, svakih 2,50 m.

### Inputi 16.11.2026 – rampe, pješački prijelaz, kružni tok
* **Spoj garažne rampe sa saobraćajnicom** je uvijek u radijusu: najmanje luk koji na oba kraka odsijeca 1,00 m
  (luk u zakošenju 1,00 × 1,00 m pod 45°). Naredba pita radijus kad rampa počinje ili završava uz ulicu; mijenja se
  i u Svojstvima („Radijus spoja sa saobraćajnicom“, 1,00–10,00 m).
* **Rampa za invalide** se usijeca u trotoar 40 cm kad je trotoar širok do 120 cm, 5 cm više za svakih 10 cm širine
  preko 120 cm, a najviše 100 cm (npr. trotoar 160 cm → 60 cm, 200 cm → 80 cm, 240 cm i više → 100 cm).
* **Biciklistička staza obilazi rampu**: na pravom dijelu ulice staza se kod rampe blago pomjeri prema trotoaru i
  vrati (prijelaz dužine najmanje 2,5 m sa svake strane), ako trotoaru pored nje ostaje najmanje 90 cm. Gdje
  mjesta nema ili je ulica u krivini, rampa prelazi preko staze kao i do sada.
* **Pješački prijelaz** ima izbor: samo pješački ili **s biciklističkom stazom 80 cm** lijevo od prijelaza (strana
  na kojoj prijelaz počinje u smjeru osovine). Staza je plava (RAL 5010, 30 % providno), u tlocrtu i u 3D; rampe
  za invalide su tada široke 2,00 m (1,20 + 0,80) i pokrivaju stazu i prvih 1,20 m prijelaza. Uključuje se i u
  Svojstvima prijelaza.
* **Garažna rampa**: pitanje „grijana rampa“ je izbačeno iz naredbe.
* **Kružni tok**: poluprečnik srednjeg otoka 10–30 m; pitanje „Smjer prvog kraka“ je uklonjeno (prvi krak ide
  udesno); pitanje o širini razdjelnog otoka je uklonjeno – razdjelni otoci su uvijek istog oblika: prema prstenu
  2,00 m dug, 1,00 m širok uz prijelaz i 1,20 m na zaobljenom kraju, zatim prijelaz 3,00 m, pa otok dug 1,00 m,
  širok 0,60 m uz prijelaz i 0,40 m na okruglom kraju (razdjelni pojas kraka 1,50 m).
* **3D model garažne rampe** (SketchUp) je isti kao u dodatku „Garažne rampe“: ploča je zatvoreno tijelo (gornja i
  donja površina, bokovi, čela), potporni zidovi su zatvorena tijela s dnom, na spiralnoj rampi zid ne ulazi u
  zavoj iznad, a ograda stoji samo gdje zid izlazi na vrh. Ivice između trouglova na kosim i zakrivljenim
  površinama (rampe i ulice s visinama) su omekšane, pa površina izgleda glatko kao kod ulice.

### Garažne rampe – dorada
* Ivice, žute linije i srednja linija zakrivljenih rampi su pravi lukovi (ne izlomljene linije), a rampa ima sivu
  površinu kao kolovoz.
* Tekst „L=…, h=…, …%“ stoji tačno na srednjoj liniji, koja je na tom mjestu prekinuta; na zakrivljenoj rampi
  tekst je podijeljen u dijelove koji prate luk i uvijek se čita slijeva nadesno. Na kratkoj rampi tekst i strelice
  se smanje da stanu.
* Strelice prate krivinu trake; na jednosmjernoj rampi srednja linija je prekinuta oko strelica; duge rampe
  dobijaju strelicu otprilike svakih 20 m.
* Srednje ostrvo 60 × 250 cm samo na dvosmjernoj pravolinijskoj rampi, 50 cm od donjeg kraja, sa prekidom
  srednje linije.
* Širina trake i zaštitni razmak drže se dozvoljenih granica i kad se promijene u Svojstvima; upozorenje kad je
  unutrašnji poluprečnik manji od 5 m.
* U 3D (SketchUp): ploča rampe s bokovima, žute i bijele linije i strelice na kosini, ostrvo kao uzdignut blok.
* **Prijelazne dionice**: kad je prosječni nagib preko 10 %, naredba nudi dionice na vrhu i dnu (zadano 4,00 m)
  s pola nagiba. Na rampi su označene poprečnom linijom i tekstom „4,00 m 7,50 %“; nagib u sredini (najveći) se
  ispisuje i provjerava prema 17 %. Dužina se mijenja u Svojstvima (0 = bez), a 3D prati prelome nagiba.
* **Rampa uz ulicu**: kad rampa počinje ili završava uz ulicu (do 1,5 m od njene vanjske ivice), ivičnjak,
  biciklistička staza i trotoar se sami otvore u širini rampe, a kolovoz se produži do rampe.

### Inputi 11.11.2026 – ulica s visinama
* U prozoru **Ulica** jedina vrsta je „Saobraćajnica unutar naselja“; stari izbor profila je uklonjen, sva ostala
  polja (smjer, trake, širine, trotoari, staze, radijus, strelice) ostaju.
* **Visine pri crtanju osovine**: nakon prve tačke upisuje se njena visina u odnosu na osnovnu (+X,XX ili −X,XX),
  zatim se bira jesu li ostale tačke iste ili različite visine. Kod različitih se visina pita nakon svake tačke
  (nagib preko 10 % odmah se javlja). Uz svaku tačku se crta simbol kote s visinom iznad linije, i kad su sve iste.
  Ulica koja počinje na postojećoj saobraćajnici nudi njenu visinu na tom mjestu.
* Garažna rampa koja počinje uz saobraćajnicu nudi visinu saobraćajnice na tom mjestu kao visinu početka.

### Prikaz pri crtanju
* Dok se klikaju tačke **ulice**, uz kursor se vidi cijela ulica kakva će nastati: ivice kolovoza, ivičnjaci,
  biciklističke staze, trotoari, zaobljene krivine i linije traka.
* Prikaz ulice uključuje i **raskrsnice** s ulicama i cestama koje nova osovina siječe, dodiruje ili im priđe:
  zaobljeni uglovi i prekinuti trotoari vide se prije nego što se ulica nacrta.
* Dok se crta **garažna rampa** (sve četiri vrste), vide se njene ivice, žute linije, srednja linija, strelice i
  ostrvo. Kod polukružne i spiralne smjer okretanja se sada pita prije početne tačke, a kod kombinovane
  poluprečnik krivina prije tačaka, da prikaz bude tačan.

### Garažne rampe – presjek, svijetla visina i ostalo
* **Presjek rampe** (`SDPRAMPAPRESJEK`): uzdužni presjek pored tlocrta – ploča rampe, prelomi nagiba sa stacionažom
  i kotom, dužina i nagib svake dionice, ploča iznad rampe i svijetla visina. Prati izmjene rampe; uklanja se
  brisanjem svoje oznake.
* **Svijetla visina**: zada se gdje počinje ploča iznad rampe i njena donja ivica; najmanja visina ispod nje se
  ispisuje u tlocrtu i presjeku, a ispod potrebne (zadano 2,10 m) je crvena uz UPOZORENJE.
* **Blagi dio pred ulicom**: dionica na vrhu s najviše 5 %; preporuka se ispisuje kad rampa izlazi na ulicu strmije.
* **Zaobljeni uglovi** otvora u ivičnjaku gdje rampa izlazi na ulicu (R = 1,50 m).
* **Kombinovana rampa**: prave dionice spojene lukovima jednog poluprečnika, jedan nagib i jedan tekst; poprečni
  nagib 3 % samo u lukovima.
* **Odvodnja**: linijska rešetka na dnu (ili na dnu i vrhu), u tlocrtu i 3D. **Grijana rampa**: šrafura vozne
  površine. **Strelica nagiba** (nagore) uz tekst.
* **3D**: potporni zidovi uz dio rampe ispod nivoa vrha i ograda 1,00 m na njima.

### Spojevi ulica (provjera 40 situacija)
* Ulica nastavljena drugom ulicom pod uglom (kraj na kraj) dobija krivinu kao jedna ulica: unutrašnja i vanjska
  strana su zaobljene i koncentrične, bez izbočine i zasjeka na vanjskom uglu, i bez šava na trotoaru.
* Gdje ulica sa stazom ili trotoarom dolazi na ulicu ili cestu koja ih nema (pristupna cesta, trotoar s jedne
  strane, biciklistička staza samo na jednoj ulici, krak kružnog toka), staza i trotoar prate ugao i završavaju
  ravno na kraju zaobljenja, umjesto nepravilnog „uha“.
* U testovima je pregled 40 situacija spajanja (T, koso, Y, zvijezda, kružni tok, blok, krivine uz raskrsnicu…),
  koji se pri svakoj izgradnji iscrtava u `ceste-pregled` na grani s gotovim paketima.

### Saobraćajnice 3: presjek, rampe, mini kružni tok, visine i garažne rampe
* Redoslijed presjeka: kolovoz | ivičnjak 15 cm | biciklistička staza | trotoar | ivičnjak 10 cm. Kad postoji
  biciklistička staza, ivičnjak 15 cm je samo između kolovoza i staze; staza i trotoar su na istoj visini (i u 3D).
  Sve linije u DWG-u su na Z = 0,00; mjerilo 1 m = 100 cm.
* **Rampe za invalide 120 cm** (po modelu „Curb Ramp“: ivičnjak spušten do kolovoza na 1,20 m, sa svake strane
  0,80 m prijelaza na punu visinu, površina se od spuštenog ivičnjaka penje do trotoara na dubini do 2,00 m;
  ivičnjak rampe je žut) ne crtaju se same uz ulice: dobija ih svaki pješački prelaz na oba kraja (gdje je trotoar), a
  bilo gdje drugo dodaju se ručno naredbom **Rampa za invalide** (`SDPRAMPAINVALIDI`): klik na trotoar daje rampu
  s te strane, klik na kolovoz s obje strane. Rampa se uklanja brisanjem njene plave oznake.
* **Mini kružni tok** (do 25 km/h): srednji otok poluprečnika 15–30 m (zelenilo), prijelazni prsten 1,20 m sa
  šrafurom AR-B88, trake prstena kao ulazne trake krakova, isprekidane linije i strelice, razdjelni otoci 10 m
  prekinuti na pješačkom prelazu i rampama, linije i trokuti „ustupi prednost“. Sve na sloju „Saobraćajnice“.
* **Visine saobraćajnica** (`SDPVISINECESTA`): ravan teren na X m n.v. ili neravan s osnovnom visinom.
  **Kota saobraćajnice** (`SDPKOTACESTE`): simbol (krug u četvrtinama, linija, ±0,00 iznad i m n.v. ispod) u odnosu
  na osnovnu visinu, za cijelu širinu ulice s trotoarima i stazama. Između kota visina se mijenja linearno; uzdužni
  nagib se ispisuje uz ulicu, a preko 10 % se odmah javlja upozorenje. U 3D (SketchUp) površine prate visine
  (mreža 50 × 50 cm); lukovi imaju najmanje 30 segmenata na 10 m.
* **Garažna rampa** (`SDPGARAZNARAMPA`, sloj „Garažna rampa“): pravolinijska (traka 2,5–3,5 m, srednje ostrvo
  60 × 250 cm na dnu po izboru), polukružna (traka 3,5–5,0 m, zaštitni razmak 25–50 cm) ili kružna/spiralna;
  jednosmjerna ili dvosmjerna. Crta se srednja linija, žute linije zaštitnog razmaka, strelice smjerova, kote
  početka i kraja i tekst „L=…m, h=…m, …%“. Nagib preko 17 % se crveno označi i javi; zakrivljena rampa u 3D ima
  poprečni nagib 3 % prema unutra.

### Ulice i ceste (dio alata, panel Crtanje)
* **Ulica** (`SDPULICA`): novi element s punim poprečnim profilom. U prozoru profili (stambena, sabirna, bulevar,
  jednosmjerna, kolni prilaz), jednosmjerna ili dvosmjerna, 1–3 trake po smjeru, širina trake, trotoar lijevo/desno
  (0–5 m, tamno sivi, 30 % prozirnosti), biciklistička staza uz kolovoz prije trotoara (0–2 m, bordo, 30 %
  prozirnosti), radijus ivičnjaka i strelice smjera (svakih 40 m). Uz kolovoz ivičnjak 15 cm, uz vanjsku ivicu
  trotoara 10 cm; isprekidane linije traka (puna sredina kod 2 + 2). Sve na sloju „Saobraćajnice“. Ulica se crta
  ili se odabere postojeća linija.
* **Sve ceste alata čine jednu mrežu**: ulica, interna pristupna saobraćajnica, kolni prilaz i servisni put iz menija
  „Ceste i biciklisti“ crtaju se zajedno i više se ne preklapaju. Na raskrsnicama i T-spojevima uglovi se zaobljuju
  radijusom ivičnjaka (stambena zona do 40 km/h, putnička i manja teretna vozila; podrazumijevano 6 m). Efektivni
  radijus prati stvarni (stvarni + biciklistička staza); radijus se vidi kad kursor stoji na ivičnjaku. Kraća
  osovina koja staje ispred druge ceste sama se produži do nje; linije traka i strelice staju prije raskrsnice.
* **Zakrivljenja kao u igri Sims**: oštar prelom osovine ulice sam se zaobli tako da unutrašnji ivičnjak dobije
  radijus ivičnjaka; unutrašnja i vanjska strana krivine su zakrivljene, a kolovoz, staza, ivičnjak i trotoar ostaju
  paralelni (koncentrični). I nacrtana osovina postaje luk. Kriva nacrtana s više tačaka postaje jedna glatka krivina
  (bez talasa), a ivice blokova između ulica i cesta različitog presjeka ostaju paralelne.
* Dugi meniji elemenata na traci (Parter i ograde, Mobilijar i oprema, Voda i instalacije, Zelenilo...) otvaraju
  podmenije po kategoriji i potkategoriji, pa sve stavke stanu na ekran.
* Kraj ulice je uvijek ravan (bez zavrnutog trotoara na jednoj strani), i kad je zadnji klik malo skrenut.
* Ulica u crtežu koji već ima elemente više ne mijenja mjerilo crteža (1 m = 100 cm postavlja samo u praznom crtežu).
* **Kružni tok** (`SDPKRUZNITOK`): prsten je sada ulica u jednom smjeru (suprotno kazaljci) s ivičnjacima i trotoarom
  izvana, a krakovi su ulice s trotoarima. Ulice i ceste nacrtane do kružnog toka same se spoje sa zaobljenim
  uglovima, bez preklapanja; linije traka staju i na razdjelnim ostrvima.
* Nakon pomjeranja, rastezanja, kopiranja, brisanja ili izmjene u Svojstvima (`SDPEDIT`) mreža se sama osvježi.
  **Uredi ulicu** (`SDPULICAUREDI`) otvara isti prozor za postojeću ulicu ili cestu; **Osvježi ceste**
  (`SDPCESTEOSVJEZI`) ručno. Osovine iz prve verzije pretvaraju se u ulice.
* **Pješački prijelaz** (`SDPPJESACKIPRIJELAZ`) je sada element: klik na cestu, uzima broj i širinu traka; bijele
  pruge 0,50 m s razmakom 0,50 m i kote; linije traka i strelice se na prijelazu prekidaju.
* Tabele i legenda dobijaju redove Kolovoz, Biciklistička staza, Ivičnjak 15 cm, Trotoar i Ivičnjak 10 cm; isti
  elementi, linije i strelice idu u SketchUp paket.
* Crtež bez projekta dobija mjerilo 1 m = 100 cm. Rasvjeta se dodaje posebno (Rasvjeta).

### Analize za investitora i inspekciju
* **Urbanistički pokazatelji** (`SDPPOKAZATELJI`, Provjera i izvoz): koeficijent izgrađenosti i iskorištenosti,
  procenat zelenih i vodopropusnih površina, broj parking mjesta, mjesta za osobe s invaliditetom i mjesta s
  punjačem. Zahtjeve regulacionog plana upisujete u prozoru (čuvaju se u projektu); uz svaki pokazatelj stoji
  ocjena „ispunjeno“ (zeleno) ili „NIJE ispunjeno“ (crveno). Tabela se postavlja klikom, a ponovno pokretanje je
  zamjenjuje.
* **Prezentacija u boji** (`SDPPREZENTACIJA`, Dokumentacija): jednim klikom travnjak i zasadi u zelenim tonovima,
  popločanje i ceste u boji svog materijala, voda plava, stabla i grmlje s krošnjom, odsjajem i sjenom, objekti
  svijetli s bačenom sjenom. Boje su na slojevima VU-PREZ-* ispod crteža; opcija Ukloni ih briše.
* **Prohodnost vozila** (`SDPPROHODNOST`, Provjera i izvoz): stvarna provjera kretanja vatrogasnog vozila, kamiona
  za smeće, dostavnog ili putničkog vozila po odabranoj ili nacrtanoj putanji (i kroz kružni tok). Crta obrise
  karoserije i tragove točkova, a crvenim krugom i porukom označava krivine preoštre za vozilo, udare karoserije u
  drveće, stubove, opremu, objekte i zidove, te prelazak točkova preko srednjeg ostrva, travnjaka i zasada.

### Prepoznatljivi simboli u tlocrtu
* 69 vrsta opreme koje su do sada imale opći znak (pravougaonik ili krug s krstom) dobilo je vlastiti simbol pogleda
  odozgo u stvarnoj veličini: tobogan, dječija sprava, penjalica, fitness sprava, gol, trampolin, kontejner, korpa,
  toplotna pumpa i druge jedinice s ventilatorom, punjači za auta i bicikle, ormari, zidne i podne svjetiljke,
  žardinjere, skulpture, znakovi, kiosk, interfon i ostalo. Vlastiti simbol dobili su i ležaljka, stolica, stol za
  stoni tenis, šahovski sto, košarkaški koš, vanjski tuš, kamera, zvučnik i Wi-Fi tačka.
* Postojeći crteži dobijaju nove simbole naredbom **Popravi oznake**.
* Nova naredba **Pregled simbola** (`SDPSIMBOLI`, Napredno): list sa svim simbolima i nazivima.

### Jednostavnije za korištenje
* **Ulica** (`SDPULICA`): cijeli profil ulice iz jedne osovine – kolovoz, ivičnjaci, trotoari, biciklističke staze,
  drvoredi i rasvjeta. Profili Stambena, Sabirna, Bulevar, Pješačka, Prilaz; širina kolovoza se pita.
* **Kružni tok** (`SDPKRUZNITOK`): prsten, ivičnjaci, srednje ostrvo sa zelenilom, krakovi s razdjelnim ostrvima,
  pješačkim prelazima, strelicama i saobraćajnim znakovima.
* **Dokumentuj list** (`SDPDOKUMENTUJ`): jednim klikom oznake, površine, širine, legenda, tabele, sjever, razmjernik
  i sastavnica na najmanjem listu koji stane (A3–A0); tabele idu desno od legende ili ispod plana.
  Ponovno pokretanje zamjenjuje list bez dupliranja.
* **Novi projekat u jednom prozoru** umjesto osam pitanja u komandnoj liniji; na ravnom 2D crtežu nulta kota
  automatski bira relativne Z.
* **Izbor tipa sa slikama**: kad naredba treba tip, otvara se lista s pretragom, kategorijama, nedavnim i omiljenim –
  nema više kucanja id-ova. Dugmad trake i dvoklik u biblioteci odmah predaju odabrani tip.
* **Širina se pita pri crtanju** staza, puteva, kanala i ivičnjaka (`SDPDRAW`, `SDPCONVERT`); zadnja širina po tipu
  se pamti.
* **Decimalni zarez svuda** u komandnoj liniji (npr. `4,5`), uz tačku.
* **Traka sa 6 panela** (Projekat, Crtanje, Izmjena, Dokumentacija, Provjera i izvoz, Pomoć) i padajući meni
  **Napredno** za rijetke naredbe; padajući meniji tipova po kategorijama.
* **Svojstva se mijenjaju odmah** na kartici Svojstva (Enter, izlaz iz polja ili izbor) – bez dugmeta Primijeni.
* **Oznake se ne preklapaju**: oznaka koja bi pala preko druge pomjera se dijagonalno.
* **Mirnija provjera**: iste poruke su grupisane (×N), stavke „za stručni pregled“ su skrivene dok se ne uključe,
  dvoklik zumira na sve elemente grupe.
* Ulica: rasvjeta u istom pojasu kao drvored postavlja se između stabala (ranije je padala na stabla i javljala kolizije).
* Provjera grupiše stavke po šifri (npr. sve kolizije u jednu stavku ×N); dugme Biblioteka uvijek otvara karticu Biblioteka.
* **Prvi koraci** (`SDPPRVIKORACI`, kartica u paleti): 8 koraka od projekta do izvoza koji se sami štikliraju;
  dvoklik pokreće korak.

### Ispravljeno
* AutoCAD 2021: površine sa šrafurom (22 tipa) nisu se mogle napraviti (`eInvalidInput`); `SDPSAMPLE` i `SDPDRAW` su padali.
* `SDPDRAW`: nakon greške više ne ostaje zaostala polilinija.
* Ključne riječi s istim početnim slovom dobile su različite skraćenice (npr. `PRavougaonik`).
* `SDPUVOZPAKETA`: element zamijenjen paketom vraća se ako novi ne uspije; režim kota čita se iz paketa.
* SketchUp: površine bez tri različita tjemena ili bez površine odbijaju se jasnom porukom (SDP-G005/G006),
  a granica koja siječe samu sebe javlja upozorenje (SDP-G007).

* Kartica trake se vraća kad je AutoCAD ukloni pri ponovnom učitavanju menija.
* Sigurnosna kopija sačuvanog (otvorenog) crteža više se ne prijavljuje kao neuspjela.
* Provjera ulaznog DWG-a i mapiranje slojeva ne ubrajaju slojeve samog dodatka.
* `SDPZOOM`/`SDPSETPARAM` na pogrešnom handleu, `SDPEDIT` s više tipova, `SDPKOTEIZOZNAKA` na „Ne“.
* Nakon klika na dugme trake ili palete odgovori koje korisnik kuca sada stižu u komandnu liniju (fokus se vraća crtežu).
* `SDPDATUM` → NultaKota na ravnom 2D crtežu (sve na Z = 0) tretira Z kao relativne; ranije je cijeli model u paketu
  završavao npr. 520 m ispod nulte kote, a provjera je javljala SDP-G012 za svaki element.
* Korisničke postavke se zapisuju atomski; nestanak struje više ne ostavlja oštećen `preferences.json`.
* Paleta se pri prvom otvaranju sidri desno umjesto da kao mali prozor prekrije dugmad trake.

### Dodano
* GitHub Actions: izgradnja i testovi pri svakom slanju; gotovi paketi na grani `builds/<grana>`.

## 0.2.0 – 29.10.2026.

### Dodano
* **Podrška za AutoCAD 2020–2024:**
  * buildovi R23 (net47) i R24 (net48), uz postojeće R25 (net8) i R26 (net10);
  * zajednička biblioteka se gradi za net47 i net8.0.
* **Bosanski jezik:**
  * cijeli korisnički interfejs i katalog;
  * kartica trake **VANJSKO UREĐENJE PRO** sa 16 panela;
  * decimalni zarez;
  * slojevi `VU-…` i SketchUp oznake `VU …`.
* **Uvoz DWG-a:**
  * mapiranje slojeva na uloge (`SDPMAPIRANJE`), s predlošcima `*.sdpmap.json`;
  * provjera ulaznog crteža sa šiframa `SDP-I…` (`SDPPROVJERADWG`);
  * preuzimanje parcele i tlocrta (`SDPPREUZMI`).

  Nazivi izvornih slojeva se ne mijenjaju, a oznaka objekta se ne pretvara automatski u tlocrt.
* **Kote:**
  * nulta kota objekta, kota partera i spuštanje saobraćajnica (`SDPDATUM`). Spuštanje nema podrazumijevanu
    vrijednost;
  * kota za pojedinačni objekat s pregledom (`SDPKOTA`);
  * prijedlog kota iz tekstualnih oznaka s prikazom veza i potvrdom (`SDPKOTEIZOZNAKA`).
* **Georeferenciranje:**
  * kontrolne tačke (`SDPGEOREF`): jedna tačka daje samo položaj, dvije ili više daju transformaciju s
    odstupanjima;
  * historija transformacija;
  * Gauss-Krüger zone 5–7 (MGI 1901), UTM, ETRS89;
  * KML za orijentaciju (`SDPKML`).
* **Teren:**
  * TIN iz tačaka, izohipsi i prelomnica (`SDPTEREN`);
  * profil (`SDPPROFIL`);
  * orijentaciona analiza nagiba, depresija i kubatura (`SDPANALIZATERENA`);
  * uvoz terena u SketchUp.
* **Provjere lokacije:**
  * `SDP-C020` element izvan parcele;
  * `SDP-E010` saobraćajnica iznad partera;
  * `SDP-E011` dodijeljena kota bez datuma;
  * `SDP-E012` objekat pomjeren nakon dodjele kote.
* **Sigurnost:**
  * provjerene sigurnosne kopije prije većih operacija, s pravilima čuvanja;
  * dnevnik oporavka (`SDPKOPIJA`, `SDPKOPIJEPOSTAVKE`, `SDPOPORAVAK`);
  * isto u SketchUpu (*Sigurnosne kopije i oporavak...*);
  * veće operacije u AutoCAD-u su jedan korak `UNDO`.
* **Shema razmjene 1.1.0:** kote, georeferenca i `terrain.json`. Paketi 1.0.0 se i dalje čitaju.
* **Pomoć:** `SDPPOMOC` otvara bosansku dokumentaciju; matrica kompatibilnosti; registar licenci.

### Promijenjeno
* Minimalna verzija SketchUpa je 2022.
* Postavke projekta imaju verziju 2. Verzija 1 se nadograđuje bez izmišljanja kota.
* Količine razlikuju izmjerene i procijenjene (`~`) vrijednosti. CSV koristi separator `;`.

### Poznati problemi
* Nije pokrenuto u AutoCAD-u ni u SketchUpu – pogledajte `docs/KOMPATIBILNOST.md` i `docs/OGRANICENJA.md`.

## 0.1.0 – 2026-10-29 (Phase 1 and vertical slice)

### Added
* Shared exchange format (schema 1.0.0): `manifest.json`, `geometry.json`, `assets/`, `preview/`; JSON schemas;
  reverse change file for SketchUp → AutoCAD.
* Shared catalog: 254 element types in 16 categories, 43 parametric builders, 38 layers, 59 materials,
  model groups, schedules.
* `SiteDesignPro.Core` (C#): model, units, geometry, package writer/reader, content hashing, validation,
  quantities and schedules, layout generators, 2D symbols, sample residential complex; `sdp` CLI.
* AutoCAD plug-in (2025/2026 R25, 2027 R26): ribbon tab with 20 panels, palette, 43 commands for project setup,
  placement, drawing, conversion, editing, annotation, schedules, validation, export and applying SketchUp changes.
* SketchUp extension: package import with preview, three levels of detail, update with change detection and
  policies, tags, scenes, attributes, element properties, library/settings dialog, change export.
* Build pipeline, packaging (`.bundle`, `.rbz`), tests, documentation.

### Known issues
* Not yet loaded in AutoCAD or SketchUp – see `docs/OGRANICENJA.md`.
