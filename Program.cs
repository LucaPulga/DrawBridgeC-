﻿/*
 * Autore: Pulga Luca
 * Classe: 4^L
 * Data di inizio: 2021-01-16
 * Data di fine:   2021-02-20
 * Scopo: Utilizzo di SemaphoreSlim per simualare l'azione di un ponte elevatoio 
 *        che permette il passaggio di una certa portata max in numero di auto
 *        consecutive in un unico senso di marcia alternato.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Media;
using System.Runtime.InteropServices;

namespace PonteElevatoio
{
    class Program
    {
        #region CDC.
        const int NUM_AUTO_SUL_PONTE = 4; // Portata max del ponte in numero di auto.
        const int MAX_X_SENSO = 7; // Numero max di auto consecutive in un senso di marcia.

        static object _lock = new object(); // Lock per la sezione critica.

        static List<string> parcheggioAutoDX = new List<string>(); // Auto a destra.
        static List<string> parcheggioAutoSX = new List<string>(); // Auto a sinistra.

        static SemaphoreSlim semaphore = new SemaphoreSlim(NUM_AUTO_SUL_PONTE); // Determina quante auto possono stare sul ponte contemporaneamente.

        static Thread t = new Thread(TitleBar); // Animazione title bar.
        //static Thread ship = new Thread(Ship); // Animazione barca.

        static int contDx = 0; // Numero auto a destra.
        static int contSx = 0; // Numero auto a sinistra.

        static bool inTransito = false; // Controlla se stanno passando sul ponte delle auto.
        static bool shipRunning = false; // Controlla se la nave sta passando.
        #endregion

        #region No resize mode.
        private const int MF_BYCOMMAND = 0x00000000;
        public const int SC_CLOSE = 0xF060;
        public const int SC_MINIMIZE = 0xF020;
        public const int SC_MAXIMIZE = 0xF030;
        public const int SC_SIZE = 0xF000;

        // Significa che il metodo dichiarato sottostante non fa parte del .NET, ma è esterno e nativo di un file dll.
        [DllImport("user32.dll")] // Per accedere a impostazioni di sistema di windows, rendendole integrabili nel codice.
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        // Indica che una DLL (Dynamic Link Library) [user32.dll] non gestita espone il metodo dell'attributo come punto di ingresso statico.
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        // Indica che una DLL (Dynamic Link Library) [kernel32.dll] non gestita espone il metodo dell'attributo come punto di ingresso statico.
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();
        #endregion

        /// <summary>
        /// Avvio della simulazione.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            #region No resize mode.
            IntPtr handle = GetConsoleWindow();
            IntPtr sysMenu = GetSystemMenu(handle, false);

            if (handle != IntPtr.Zero)
            {
                // DeleteMenu(sysMenu, SC_CLOSE, MF_BYCOMMAND); // Previene la chiusura della console dall'utente.
                DeleteMenu(sysMenu, SC_MINIMIZE, MF_BYCOMMAND); // Previene che l'utente minimizzi la finstra.
                DeleteMenu(sysMenu, SC_MAXIMIZE, MF_BYCOMMAND); // Previene che l'utente maximizzi la finstra.
                DeleteMenu(sysMenu, SC_SIZE, MF_BYCOMMAND); // Previene il resizing della finestra.
            }
            #endregion

            t.Start();
            char opz = ' ';
            Console.CursorVisible = false;
            Console.SetWindowSize(120,46); // Dimensione finestra all'avvio.

            Background(); // Setta lo sfondo.

            while (true)
            {
                Scelta(out opz); // Richiede l'opzione.

                switch (opz) // Esegue l'opzione scelta.
                {
                    #region Aggiunge auto a sinistra.
                    case 'L':
                        NuovoVeicoloSx("AutoSx"); // Aggiunge in coda di attesa un'auto a sinistra.
                        break;
                    #endregion

                    #region Aggiunge camion a sinistra.
                    case 'Q':
                        NuovoVeicoloSx("CamionSx"); // Aggiunge in coda di attesa un'auto a sinistra.
                        break;
                    #endregion

                    #region Aggiunge auto a destra.
                    case 'R':
                        NuovoVeicoloDx("AutoDx"); // Aggiunge in coda di attesa un'auto a destra.
                        break;
                    #endregion

                    #region Aggiunge camion a destra.
                    case 'W':
                        NuovoVeicoloDx("CamionDx"); // aggiunge in coda di attesa un'auto a sinistra.
                        break;
                    #endregion

                    #region Passaggio veicoli.
                    case 'P':
                        PassaggioSulPonte(); // Avvia la simualazione del passaggio dei veicoli.
                        break;
                    #endregion

                    #region Passaggio nave.
                    case 'S':
                        if (inTransito == false /*&& ship.ThreadState == ThreadState.Background*/)
                        {
                            shipRunning = true;
                            Ship();
                            //ship.Start(); // Passaggio della barca.
                            //shipRunning = true;
                        }
                        //else if (inTransito == false /*&& !(ship.ThreadState == ThreadState.Running*//* && shipRunning == false*/)
                        //{
                        //    //ship = new Thread(Ship);
                        //    //ship.Start();
                        //}
                        else
                        {
                            Console.CursorLeft = 50;
                            Console.CursorTop = 15;
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.SetCursorPosition(34, 13);
                            Console.WriteLine("-----------------------------------------------------");
                            string mex = "AUTO IN TRANSITO: LA NAVE PARTIRA' APPENA FINITO IL TRANSITO";
                            Console.SetCursorPosition(34, 17);
                            Console.WriteLine("-----------------------------------------------------");
                            Console.SetCursorPosition(28,15);
                            Console.WriteLine(mex);
                        }
                        break;
                    #endregion

                    #region Uscita app.
                    case 'E':
                        t.Abort();
                        Environment.Exit(-1); // Chiusura app.
                        break;
                    #endregion
                }
            }
        }

        #region Aggiunge veicolo al parcheggio SX.
        /// <summary>
        /// Aggiunge veicolo nel parcheggio a sinistra.
        /// </summary>
        static void NuovoVeicoloSx(string veicoloSx)
        {
            if (veicoloSx == "AutoSx")
            {
                veicoloSx = "AutoSx" + contSx++;
                parcheggioAutoSX.Add(veicoloSx); // Aggiunge veicolo in attesa a sinsitra.
            }
            else
            {
                veicoloSx = "CamionSx" + contSx++;
                parcheggioAutoSX.Add(veicoloSx); // Aggiunge veicolo in attesa a sinsitra.
            }
            StampaVeicoliSinistra();
            StampaVeicoliDestra();
        }
        #endregion

        #region Aggiunge veicolo al parcheggio DX.
        /// <summary>
        /// Aggiunge veicolo nel parcheggio a sinistra.
        /// </summary>
        static void NuovoVeicoloDx(string veicoloDx)
        {
            if(veicoloDx == "AutoDx")
            {
                veicoloDx = "AutoDx" + contDx++;
                parcheggioAutoDX.Add(veicoloDx); // Aggiunge veicolo in attesa a destra.
            }
            else
            {
                veicoloDx = "CamionDx" + contDx++;
                parcheggioAutoDX.Add(veicoloDx); // Aggiunge veicolo in attesa a destra.
            }
            StampaVeicoliSinistra();
            StampaVeicoliDestra();
        }
        #endregion

        #region Stampa veicoli sinistra.
        /// <summary>
        /// Stampa e aggiorna la auto in attesa a sinistra.
        /// </summary>
        static void StampaVeicoliSinistra()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            // Elenca le auto di sinistra.
            for (int i = 0; i < parcheggioAutoSX.Count; i++)
            {
                Console.SetCursorPosition(2, 5 + i);
                Console.WriteLine(parcheggioAutoSX[i]);
            }
        }
        #endregion

        #region Stampa veicoli destra.
        /// <summary>
        /// Stampa e aggiorna la auto in attesa a destra.
        /// </summary>
        static void StampaVeicoliDestra()
        {
            // Elenca le auto di destra.
            Console.ForegroundColor = ConsoleColor.Green;
            for (int i = 0; i < parcheggioAutoDX.Count; i++)
            {
                Console.SetCursorPosition(105, 5 + i);
                Console.WriteLine(parcheggioAutoDX[i]);
            }
        }
        #endregion

        #region Attraversamento verso sinistra.
        /// <summary>
        /// Attraversamento della auto di destra verso sinistra.
        /// </summary>
        /// <param name="auto">Targa dell'auto che transita.</param>
        /// <param name="riga">Corsia dell'auto.</param>
        /// <returns></returns>
        static async Task AttraversaVersoSx(string targaAuto, int corsiaAuto)
        {
            if (targaAuto.Contains("CamionDx"))
            {
                lock (_lock)
                {
                    TruckVersoSx(); // Attraversamento camion uno alla volta verso sinistra.
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                semaphore.Wait(); // Solo il numero specificato da NUM_AUTO_SUL_PONTE ovver ortata max del ponte in numero di auto, può entrare.
                for (int i = 0; i < 60; i++)
                {
                    lock (_lock) // Solo un'auto alla volta entra nella sezione critica.
                    {
                        Console.CursorTop = 25 + corsiaAuto;
                        Console.CursorLeft = 85 - i;
                        Console.Write(targaAuto);
                        Console.CursorLeft = 84 - i + targaAuto.Length;
                        Console.Write(" ");
                        Console.CursorLeft = 84 - i;
                        Console.Write(targaAuto);
                    }
                    await Task.Delay(80); // Aggiornamento animazione attraversamento auto.
                }
                semaphore.Release(); // Uscita auto dal ponte.
            }
        }
        #endregion

        #region Attraversamento verso destra.
        /// <summary>
        /// Attraversamento della auto di sinistra verso destra.
        /// </summary>
        /// <param name="auto">Targa dell'auto che transita.</param>
        /// <param name="riga">Corsia dell'auto.</param>
        /// <returns></returns>
        static async Task AttraversaVersoDx(string targaAuto, int corsiaAuto)
        {
            if (targaAuto.Contains("CamionSx"))
            {
                lock (_lock)
                {
                    TruckVersoDx(); // Attraversamento camion uno alla volta verso destra.
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                semaphore.Wait(); // Solo il numero specificato da NUM_AUTO_SUL_PONTE ovver ortata max del ponte in numero di auto, può entrare.
                for (int i = 0; i < 60; i++)
                {
                    lock (_lock) // Solo un'auto alla volta entra nella sezione critica.
                    {
                        Console.CursorTop = 25 + corsiaAuto;
                        Console.CursorLeft = 25 + i;
                        Console.Write(targaAuto);
                        Console.CursorLeft = 25 + i;
                        Console.Write("  ");
                        Console.Write(targaAuto);
                    }
                    await Task.Delay(80); // Aggiornamento animazione attraversamento auto.
                }
                semaphore.Release();  // Uscita auto dal ponte.
            }
        }
        #endregion

        #region Transito delle auto in senso unico alternato.
        /// <summary>
        /// Transito delle auto in senso unico alternato.
        /// </summary>
        static void PassaggioSulPonte()
        {
            int passate = 0; // Numero di auto passate in un turno completo di 7.
            int indice = 0;
            int k = 0;
            List<string> autoInTransito = new List<string>(); // Auto in transito sul ponte: Max 4 auto, Max 1 camion.
            while (/*(!(ship.ThreadState == ThreadState.Running)) &&*/shipRunning==false && (parcheggioAutoSX.Count > 0 || parcheggioAutoDX.Count > 0)) // Sono ancora presenti auto in uno dei due parcheggi?
            {
                inTransito = true;
                passate = 0; 
                Sx:  if (parcheggioAutoSX.Count > 0 && passate < MAX_X_SENSO) // Controllo se sono rimasti veicoli nel parcheggio a sinsitra e se ne sono passati massimo 7.
                {
                    autoInTransito.Clear();
                    for (k = 0; k < MAX_X_SENSO && parcheggioAutoSX.Count > 0 && passate < MAX_X_SENSO; k++) // Massimo 7 auto del parcheggio di sinistra.
                    {
                        if(parcheggioAutoSX[parcheggioAutoSX.Count - 1].Contains("CamionSx") && autoInTransito.Count == 0) // Controllo se passa per primo un camion.
                        {
                            autoInTransito.Add(parcheggioAutoSX[parcheggioAutoSX.Count - 1]);
                            parcheggioAutoSX.RemoveAt(parcheggioAutoSX.Count - 1);
                            indice = k;
                            passate++;
                            break;
                        }
                        else if(parcheggioAutoSX[parcheggioAutoSX.Count - 1].Contains("CamionSx")) // Appena trovo un camion, stoppo il numero di auto da far passare e le faccio atraversare.
                        {
                            break;
                        }
                        else
                        {
                            autoInTransito.Add(parcheggioAutoSX[parcheggioAutoSX.Count - 1]); // Raccolgo le auto che devono passare nel turno.
                            parcheggioAutoSX.RemoveAt(parcheggioAutoSX.Count - 1); // Tolgo le auto che passano.
                            passate++;
                        }
                    }

                    Console.Clear();
                    Background();
                    StampaVeicoliSinistra();
                    StampaVeicoliDestra();

                    List<Task> attraversamentiAutoSinistra = new List<Task>(); // Task delle auto.
                   
                    for (int i = 0; i < autoInTransito.Count; i++)
                    {
                        attraversamentiAutoSinistra.Add(AttraversaVersoDx(autoInTransito[i], i % NUM_AUTO_SUL_PONTE)); // Set dei task (auto) che passano sulle varie corsie.
                    }
                    Task.WaitAll(attraversamentiAutoSinistra.ToArray()); // Tutti gli altri task aspettano il termine del passaggio delle altre auto.
                    goto Sx;
                }


                passate = 0;
                Dx:  if (parcheggioAutoDX.Count > 0 && passate < MAX_X_SENSO) // Controllo se sono rimasti veicoli nel parcheggio a destra e se ne sono passati massimo 7.
                {
                    autoInTransito.Clear();
                    for (k = 0; k < MAX_X_SENSO && parcheggioAutoDX.Count > 0 && passate < MAX_X_SENSO; k++) // Massimo 7 auto del parcheggio di sinistra.
                    {
                        if (parcheggioAutoDX[parcheggioAutoDX.Count - 1].Contains("CamionDx") && autoInTransito.Count == 0)
                        {
                            autoInTransito.Add(parcheggioAutoDX[parcheggioAutoDX.Count - 1]);
                            parcheggioAutoDX.RemoveAt(parcheggioAutoDX.Count - 1); // Tolgo le auto che passano.
                            indice = k;
                            passate++;
                            break;
                        }
                        else if (parcheggioAutoDX[parcheggioAutoDX.Count - 1].Contains("CamionDx"))
                        {
                            break;
                        }
                        else
                        {
                            autoInTransito.Add(parcheggioAutoDX[parcheggioAutoDX.Count - 1]);
                            parcheggioAutoDX.RemoveAt(parcheggioAutoDX.Count - 1); // Tolgo le auto che passano.
                            passate++;
                        }
                    }

                    Console.Clear();
                    Background();
                    StampaVeicoliSinistra();
                    StampaVeicoliDestra();

                    List<Task> attraversamentiAutoDestra = new List<Task>();

                    for (int i = 0; i < autoInTransito.Count; i++)
                    {
                        attraversamentiAutoDestra.Add(AttraversaVersoSx(autoInTransito[i], i % NUM_AUTO_SUL_PONTE)); // Set dei task (auto) che passano sulle varie corsie.
                    }
                    Task.WaitAll(attraversamentiAutoDestra.ToArray()); // Tutti gli altri task aspettano il termine del passaggio delle altre auto.
                    goto Dx;
                }
            }
            inTransito = false;
            Console.Clear();
            Background();
        }
        #endregion

        #region Background.
        /// <summary>
        /// Set del background con il fiume e il ponte.
        /// </summary>
        static void Background()
        {
            Menu(); // Mostra le possibili scelte.

            Console.ForegroundColor = ConsoleColor.Cyan;
            for (int i = 4; i < 24; i++)
            {
                Console.SetCursorPosition(42, i);
                Console.Write("░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░"); // Acqua.
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.SetCursorPosition(35, 24);
            Console.Write("═════════════════════════════════════════════════");
            Console.SetCursorPosition(35, 25 + NUM_AUTO_SUL_PONTE); // Set della distanza fra le due sponde del ponte.
            Console.Write("═════════════════════════════════════════════════");
            Console.ForegroundColor = ConsoleColor.Cyan;
            for (int i = 26 + NUM_AUTO_SUL_PONTE; i < 47; i++)
            {
                Console.SetCursorPosition(42, i);
                Console.Write("░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░");
            }
        }
        #endregion

        #region Title bar.
        /// <summary>
        /// Animazione title bar.
        /// </summary>
        static void TitleBar()
        {
            string progressbar = "Ponte levatoi senso unico alternato di Pulga Luca 4^L";
            var title = "";
            while (true)
            {
                for (int i = 0; i < progressbar.Length; i++)
                {
                    title += progressbar[i];
                    Console.Title = title; // Aggiornamento animazione titolo.
                    Thread.Sleep(100);
                }
                title = "";
            }
        }
        #endregion
        
        #region Ship.
        /// <summary>
        /// Animazione passaggio barca.
        /// </summary>
        static void Ship()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Clear();
            Menu();

            for (int i = 4; i < 50; i++)
            {
                Console.SetCursorPosition(35, i);
                Console.Write("~'^~'^~'^~'^~'^~'^~~'^~'^~'^~'^~'^~'^~~'^~'^~'^~'^~"); // Acqua.
            }
            Thread.Sleep(1000);
            for (int j = 0; j < 30; j++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.Clear();
                for (int i = 4; i < 50; i++)
                {
                    Console.SetCursorPosition(35, i);
                    Console.Write("~'^~'^~'^~'^~'^~'^~~'^~'^~'^~'^~'^~'^~~'^~'^~'^~'^~");
                }
                // Realizzazione animazione barca.
                Console.SetCursorPosition(45, 10 + j);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine("       _    _o");
                Console.SetCursorPosition(45, 11 + j);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  __|_|__|_|__");
                Console.SetCursorPosition(45, 12 + j);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" |____________|__");
                Console.SetCursorPosition(45, 13 + j);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(" |o o o o o o o o /");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.SetCursorPosition(45, 14 + j);
                Console.WriteLine(" ~'`~'`~'`~'`~'`~'`~'`~");
                Thread.Sleep(150);
            }

            Console.Clear();
            Console.CursorVisible = false;
            Background();
            StampaVeicoliSinistra();
            StampaVeicoliDestra();
            shipRunning = false;
        }
        #endregion

        #region Truck verso SX.
        /// <summary>
        /// Passaggio del camion destra verso sinistra.
        /// </summary>
        static void TruckVersoSx()
        {
            for (int j = 0; j < 50; j++)
            {
                // Animazione dell'attraversamento del ponte di un camion verso sinistra.
                Console.Clear();
                Background();
                Console.SetCursorPosition(70 - j, 23);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine(@"            __________ _______________________");
                Console.SetCursorPosition(70 - j, 24);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(@"           / /(__) || |                      |");
                Console.SetCursorPosition(70 - j, 25);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"  ________/ / |OO| || |                      |");
                Console.SetCursorPosition(70 - j, 26);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(@"(|  ____   \       ||_________||___________  |");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.SetCursorPosition(70 - j, 27);
                Console.WriteLine(@"/| / __ \   |______||     / __ \   / __ \   ||");
                Console.SetCursorPosition(70 - j, 28);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine(@"\|| /()\ |_______________| /()\ |_| /()\ |__| ");
                Console.SetCursorPosition(70 - j, 29);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"    \__/                   \__/     \__/    ");
                Thread.Sleep(150);
            }
        }
        #endregion

        #region Truck verso DX.
        /// <summary>
        /// Passaggio del camion sinistra verso destra.
        /// </summary>
        static void TruckVersoDx()
        {
            for(int j = 0; j < 60; j++)
            {
                // Animazione dell'attraversamento del ponte di un camion verso destra.
                Console.Clear();
                Background();
                Console.SetCursorPosition(5 + j, 23);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine(@"_______________________ __________");
                Console.SetCursorPosition(5 + j, 24);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(@"|                     | || (__) \ \ ");
                Console.SetCursorPosition(5 + j, 25);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"|                     | || |OO|  \ \");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.SetCursorPosition(5 + j, 26);
                Console.WriteLine(@"|___________||________ _||        \_  ____  |)");
                Console.SetCursorPosition(5 + j, 27);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine(@"||   / __ \   / __ \    ||______|    / __ \ |\");
                Console.SetCursorPosition(5 + j, 28);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(@" |__| /()\ |_| /()\ |_______________| /()\|_|/");
                Console.SetCursorPosition(5 + j, 29);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"      \__/     \__/                   \__/ ");
                Thread.Sleep(150);
            }
        }
        #endregion

        #region Scelta comando.
        /// <summary>
        /// Visualizza il menù e richiede l'opzione.
        /// </summary>
        /// <returns>Scelta dell'utente.</returns>
        static void Scelta(out char ch)
        {
            do // Legge e controlla l'opzione scelta.
            {
                ch = Console.ReadKey(true).KeyChar;
                ch = char.ToUpper(ch);
            }
            while (!((ch == 'L') || (ch == 'R') || (ch == 'S') || (ch == 'E') || (ch == 'P') || (ch == 'Q') || (ch == 'W'))); // Controllo scelta.
        }
        #endregion

        #region Menu.
        /// <summary>
        /// Menu dei comandi.
        /// </summary>
        static void Menu()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.CursorTop = 1;
            Console.CursorLeft = 25;
            Console.WriteLine("SIMULAZIONE ATTRAVERSAMENTO PONTE LEVATOI SENSO UNICO ALTERNATO.");

            Console.CursorTop = 2;
            Console.CursorLeft = 2;
            Console.WriteLine(" Add car [L]eft"); // Aggiunge auto a sinistra in coda.

            Console.CursorTop = 3;
            Console.CursorLeft = 2;
            Console.WriteLine(" [Q] Add camion left"); // Aggiunge camion a sinistra in coda.

            Console.CursorTop = 2;
            Console.CursorLeft = 99;
            Console.WriteLine(" Add car [R]ight"); // Aggiunge auto a destra in coda.

            Console.CursorTop = 3;
            Console.CursorLeft = 99;
            Console.WriteLine(" [W] Add camion right"); // Aggiunge auto a destra in coda.

            Console.CursorTop = 2;
            Console.CursorLeft = 45;
            Console.WriteLine(" Car [P]assage"); // Avvia la simulazione.

            Console.CursorTop = 2;
            Console.CursorLeft = 22;
            Console.WriteLine(" [S]hip passage"); // Chiude il ponte per il passaggio di una nave.

            Console.CursorTop = 2;
            Console.CursorLeft = 70;
            Console.WriteLine(" [E]xit from drawbridge\n"); // Esce dal programma.
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.CursorTop = 3;
            Console.CursorLeft = 24;
            Console.WriteLine("[In caso di passaggio nave, prima passeranno tutti i veicoli già in attesa]"); // Chiude il ponte per il passaggio di una nave.
        }

        #endregion
    }
}
