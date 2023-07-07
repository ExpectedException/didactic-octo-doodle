using System;
using System.IO;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using SteamAuth;
using MailKit;
using MailKit.Net.Imap;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Steam_Desktop_Authenticator
{
    public partial class LoginForm
    {
        public SteamGuardAccount androidAccount;
        public LoginType LoginReason;
        public string _mailImap;
        private readonly string _login;
        private readonly string _password;
        private string _emailLogin;
        private readonly string _emailPassword;
        private string _phoneNumber;
        private string _code;
        private string _tzId;
        private readonly string _onlineSimApiKey = File.ReadAllText("onlinesimkey.txt");


        public LoginForm(LoginType loginReason = LoginType.Initial, SteamGuardAccount account = null, string _mailImap = "", string _login = "", string _password = "", string _emailLogin = "", string _emailPassword = "")
        {
            //InitializeComponent();
            //Console.Write("dsdsd");
            this.LoginReason = loginReason;
            this.androidAccount = account;
            this._login = _login;
            this._password = _password;
            this._mailImap = _mailImap;
            this._emailLogin = _emailLogin;
            this._emailPassword = _emailPassword;
            this._code = "";

            try
            {
                if (this.LoginReason != LoginType.Initial)
                {
                    txtUsername.Text = account.AccountName;
                    txtUsername.Enabled = false;
                }

                if (this.LoginReason == LoginType.Refresh)
                {
                    labelLoginExplanation.Text = "Истек срок действия ваших учетных данных Steam. Для торговли и подтверждения рынка, чтобы работать должным образом, пожалуйста, войдите снова.";
                }
                this.btnSteamLogin_Click();
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось найти учетную запись. Попробуйте закрыть и снова открыть SDA.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //this.Close();
            }
        }

        public void SetUsername(string username)
        {
            txtUsername.Text = username;
        }

        public string FilterPhoneNumber(string phoneNumber)
        {
            return phoneNumber.Replace("-", "").Replace("(", "").Replace(")", "");
        }

        public bool PhoneNumberOkay(string phoneNumber)
        {
            if (phoneNumber == null || phoneNumber.Length == 0) return false;
            if (phoneNumber[0] != '+') return false;
            return true;
        }

        private string GetLoginCodeRambler()
        {
            Thread.Sleep(10000);

            var loginCode = string.Empty;

            using (var client = new ImapClient())
            {
                client.Connect(_mailImap, 993, true);
                client.Authenticate(_emailLogin, _emailPassword);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadWrite);

                for (var i = inbox.Count - 1; i >= 0; i--)
                {
                    var message = inbox.GetMessage(i);

                    var code = Regex.Match(message.HtmlBody, "class=([\"])title-48 c-blue1 fw-b a-center([^>]+)([>])([^<]+)").Groups[4].Value;
                    if (string.IsNullOrEmpty(code)) continue;

                    loginCode = code.Trim();
                    Log($"Login code: {loginCode}");
                    break;
                }
            }

            return loginCode;
        }

        private string WaitSmsCode()
        {
            var waiting = 0;
            while (waiting < 30)
            {
                Thread.Sleep(2000);
                waiting++;

                using (var wc = new WebClient())
                {
                    var response = wc.DownloadString($"https://onlinesim.io/api/getState.php?apikey={_onlineSimApiKey}&tzid={_tzId}&msg_list=1&orderby=desc&clean=0&message_to_code=1").Trim('[',']');
                    var json = JObject.Parse(response);

                    try
                    {
                        var msg = json["msg"].Last["msg"].ToString();

                        if (string.IsNullOrEmpty(msg))
                            continue;

                        Log($"SMS code: {msg}");

                        return msg;
                    }
                    catch 
                    {
                        try
                        {
                            var msg = json["msg"]?.ToString();
                            if (string.IsNullOrEmpty(msg))
                                continue;

                            Log($"SMS code: {msg}");

                            return msg;
                        }
                        catch { }
                    }
                }
            }

            return string.Empty;
        }
        private void ConfirmPhoneRambler()
        {
            Thread.Sleep(20000);

            using (var client = new ImapClient())
            {
                client.Connect(_mailImap, 993, true);
                client.Authenticate(_emailLogin, _emailPassword);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadWrite);

                for (var i = inbox.Count - 1; i >= 0; i--)
                {
                    var message = inbox.GetMessage(i);
                    var link = Regex.Match(message.HtmlBody, "store([.])steampowered([.])com([\\/])phone([\\/])ConfirmEmailForAdd([?])stoken=([^\"]+)").Groups[0].Value;
                    if (string.IsNullOrEmpty(link)) continue;

                    new WebClient().DownloadString("https://" + link);
                    //Log("Mail confirmed");
                    break;
                }

                client.Disconnect(true);
            }

            Thread.Sleep(22000);
        }
        private string GetNewPhoneNumber()
        {
            using (var wc = new WebClient())
            {
                //var response = wc.DownloadString($"https://onlinesim.io/api/getNum.php?apikey={_onlineSimApiKey}&service=Steam&country=372");
                var response = wc.DownloadString($"https://onlinesim.io/api/getNum.php?apikey={_onlineSimApiKey}&service=Steam&country=372");
                var json = JObject.Parse(response);

                if (json["response"]?.ToString() == "1")
                    _tzId = json["tzid"]?.ToString();
                else return string.Empty;
            }

            Thread.Sleep(5000);

            using (var wc = new WebClient())
            {
                var response = wc.DownloadString($"https://onlinesim.io/api/getState.php?apikey={_onlineSimApiKey}&tzid={_tzId}").Replace("[", string.Empty).Replace("]", string.Empty);
                var json = JObject.Parse(response);

                _phoneNumber = json["number"]?.ToString();
            }

            Log($"Got number: {_phoneNumber}");

            return _phoneNumber;
        }

        private void Log(string message) => Console.WriteLine($"[{_login}] {message}");

        private static void LogToFile(string message) => File.AppendAllText("result.txt", message + "\n");

        private void btnSteamLogin_Click()
        {
            //string username = "yzflj1120";
            string username = _login;
            string password = _password;
            //string password = "bvHDglNcqEduZoCncY";

            if (LoginReason == LoginType.Android)
            {
                FinishExtract(username, password);
                return;
            }
            else if (LoginReason == LoginType.Refresh)
            {
                RefreshLogin(username, password);
                return;
            }

            var userLogin = new UserLogin(username, password);
            LoginResult response = LoginResult.BadCredentials;


            while ((response = userLogin.DoLoginV2()) != LoginResult.LoginOkay)
            {
                switch (response)
                {
                    case LoginResult.NeedEmail:
                        Log("Waiting login code");
                        var emailCode = GetLoginCodeRambler();
                        userLogin.EmailCode = emailCode;
                        break;

                    case LoginResult.NeedCaptcha:
                        CaptchaForm captchaForm = new CaptchaForm(userLogin.CaptchaGID);
                        captchaForm.ShowDialog();
                        if (captchaForm.Canceled)
                        {
                            //this.Close();
                            return;
                        }

                        userLogin.CaptchaText = captchaForm.CaptchaCode;
                        break;

                    case LoginResult.Need2FA:
                        MessageBox.Show("К этому аккаунту уже привязан мобильный Аутентификатор.\nУдалите старый Аутентификатор из своего аккаунта Steam перед добавлением нового.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.BadRSA:
                        MessageBox.Show("Error logging in: Steam вернул \"BadRSA\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.BadCredentials:
                        MessageBox.Show("Error logging in: Username or password некорректные.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.TooManyFailedLogins:
                        MessageBox.Show("Error logging in: Слишком много неудачных попыток, попробуйте позже.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.GeneralFailure:
                        MessageBox.Show("Error logging in: Steam вернул \"GeneralFailure\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;
                }
            }

            //Login succeeded

            SessionData session = userLogin.Session;
            AuthenticatorLinker linker = new AuthenticatorLinker(userLogin);

            AuthenticatorLinker.LinkResult linkResponse = AuthenticatorLinker.LinkResult.GeneralFailure;

            while ((linkResponse = linker.AddAuthenticator()) != AuthenticatorLinker.LinkResult.AwaitingFinalization)
            {
                switch (linkResponse)
                {
                    case AuthenticatorLinker.LinkResult.MustProvidePhoneNumber:
                        {
                            string phoneNumber = string.Empty;
                            while (phoneNumber == string.Empty)
                            {
                                phoneNumber = GetNewPhoneNumber();
                                Thread.Sleep(2000);
                            }
                            while (!PhoneNumberOkay(phoneNumber))
                            {
                                InputForm phoneNumberForm = new InputForm("Введите номер телефона в следующем формате: +{cC} номер телефона. Например, +1 123-456-7890");
                                phoneNumberForm.txtBox.Text = "+1 ";
                                phoneNumberForm.ShowDialog();
                                if (phoneNumberForm.Canceled)
                                {
                                    //this.Close();
                                    return;
                                }

                                phoneNumber = FilterPhoneNumber(phoneNumberForm.txtBox.Text);
                            }
                            linker.PhoneNumber = phoneNumber;

                            if (linker._get_phone_number())
                            {
                                //MessageBox.Show("Подтвердите письмо на почте и нажмите ОК");
                                //redan
                                ConfirmPhoneRambler();
                                linker._email_verification();

                                //InputForm smsCodeForm = new InputForm("Пожалуйста, введите SMS-код, отправленный на ваш телефон.");
                                //smsCodeForm.ShowDialog();
                                //if (smsCodeForm.Canceled)
                                //{
                                //    this.Close();
                                //    return;
                                //}
                                string smsCodeForm = WaitSmsCode();
                                _code = smsCodeForm;
                                if (linker._get_sms_code(smsCodeForm))
                                {
                                    Console.Write("Номер успешно привязан к аккаунту. Продолжаем получать maFile..");
                                }
                            }
                            break;
                        }
                    case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                        linker.PhoneNumber = null;
                        break;

                    case AuthenticatorLinker.LinkResult.GeneralFailure:
                        //MessageBox.Show("Ошибка добавления телефона. Steam вернул \"GeneralFailure\".");
                        //this.Close();
                        Log($"General failure");
                        Thread.Sleep(10000);
                        continue;
                        //return;
                }
            }

            Manifest manifest = Manifest.GetManifest();
            string passKey = null;

            /*if (manifest.Entries.Count == 0)
            {
                passKey = manifest.PromptSetupPassKey("Введите (PassKey) ключ шифрования. Оставьте пустым или нажмите Отмена, чтобы не шифровать (очень небезопасно).");
            }
            else if (manifest.Entries.Count > 0 && manifest.Encrypted)
            {
                bool passKeyValid = false;
                while (!passKeyValid)
                {
                    InputForm passKeyForm = new InputForm("Введите текущий PassKey");
                    passKeyForm.ShowDialog();
                    if (!passKeyForm.Canceled)
                    {
                        passKey = passKeyForm.txtBox.Text;
                        passKeyValid = manifest.VerifyPasskey(passKey);
                        if (!passKeyValid)
                        {
                            MessageBox.Show("Этот PassKey недействителен. Введите PassKey, который вы использовали для других учетных записей.");
                        }
                    }
                    else
                    {
                        this.Close();
                        return;
                    }
                }
            }*/

            if (manifest.Encrypted)
            {
                bool passKeyValid = false;
                while (!passKeyValid)
                {
                    InputForm passKeyForm = new InputForm("Введите текущий PassKey");
                    passKeyForm.ShowDialog();
                    if (!passKeyForm.Canceled)
                    {
                        passKey = passKeyForm.txtBox.Text;
                        passKeyValid = manifest.VerifyPasskey(passKey);
                        if (!passKeyValid)
                        {
                            MessageBox.Show("Этот PassKey недействителен. Введите PassKey, который вы использовали для других учетных записей.");
                        }
                    }
                    else
                    {
                        //this.Close();
                        return;
                    }
                }
            }

            //Save the file immediately; losing this would be bad.
            if (!manifest.SaveAccount(linker.LinkedAccount, passKey != null, passKey))
            {
                manifest.RemoveAccount(linker.LinkedAccount);
                MessageBox.Show("Не удается сохранить файл Mobile authenticator. Мобильный Аутентификатор не связан.");
               // this.Close();
                return;
            }

            //MessageBox.Show("Мобильный Аутентификатор еще не подключен. Перед завершением проверки подлинности запишите код отзыва: " + linker.LinkedAccount.RevocationCode);

            AuthenticatorLinker.FinalizeResult finalizeResponse = AuthenticatorLinker.FinalizeResult.GeneralFailure;
            while (finalizeResponse != AuthenticatorLinker.FinalizeResult.Success)
            {
                /*InputForm smsCodeForm = new InputForm("Пожалуйста, введите SMS-код, отправленный на ваш телефон.");
                smsCodeForm.ShowDialog();
                if (smsCodeForm.Canceled)
                {
                    manifest.RemoveAccount(linker.LinkedAccount);
                    this.Close();
                    return;
                }*/

                /*InputForm confirmRevocationCode = new InputForm("Пожалуйста, введите код отзыва, чтобы убедиться, что вы его сохранили.");
                confirmRevocationCode.ShowDialog();
                if (confirmRevocationCode.txtBox.Text.ToUpper() != linker.LinkedAccount.RevocationCode)
                {
                    MessageBox.Show("Неверный код отзыва; средство проверки подлинности не связано.");
                    manifest.RemoveAccount(linker.LinkedAccount);
                    this.Close();
                    return;
                }
                */
                string smsCode = _code;
                while (_code == smsCode)
                {
                    Thread.Sleep(4000);
                    smsCode = WaitSmsCode();
                }
                
                finalizeResponse = linker.FinalizeAddAuthenticator(smsCode);

                switch (finalizeResponse)
                {
                    case AuthenticatorLinker.FinalizeResult.BadSMSCode:
                        continue;

                    case AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes:
                        MessageBox.Show("Не удалось создать правильные коды для завершения проверки подлинности. Аутентификатор не должен был быть связан. В случае, если это было, пожалуйста, запишите свой код отзыва, так как это последний шанс увидеть его: " + linker.LinkedAccount.RevocationCode);
                        manifest.RemoveAccount(linker.LinkedAccount);
                        //this.Close();
                        return;

                    case AuthenticatorLinker.FinalizeResult.GeneralFailure:
                        MessageBox.Show("Невозможно завершить проверку подлинности. Аутентификатор не должен был быть связан. В случае, если это было, пожалуйста, запишите свой код отзыва, так как это последний шанс увидеть его: " + linker.LinkedAccount.RevocationCode);
                        manifest.RemoveAccount(linker.LinkedAccount);
                        //this.Close();
                        return;
                }
            }

            //Linked, finally. Re-save with FullyEnrolled property.
            manifest.SaveAccount(linker.LinkedAccount, passKey != null, passKey);
            Log($"{_login}:{_password}:{_emailLogin}:{_emailPassword}:{_phoneNumber}:{linker.LinkedAccount.RevocationCode}");
            LogToFile($"{_login}:{_password}:{_emailLogin}:{_emailPassword}:{_phoneNumber}:{linker.LinkedAccount.RevocationCode}");

            //MessageBox.Show("Мобильный Аутентификатор успешно подключен. Пожалуйста, запишите код отзыва : " + linker.LinkedAccount.RevocationCode);
            //this.Close();
        }

        /// <summary>
        /// Handles logging in to refresh session data. i.e. changing steam password.
        /// </summary>
        /// <param name="username">Steam username</param>
        /// <param name="password">Steam password</param>
        private async void RefreshLogin(string username, string password)
        {
            long steamTime = await TimeAligner.GetSteamTimeAsync();
            Manifest man = Manifest.GetManifest();

            androidAccount.FullyEnrolled = true;

            UserLogin mUserLogin = new UserLogin(username, password);
            mUserLogin.TwoFactorCode = androidAccount.GenerateSteamGuardCodeForTime(steamTime);

            LoginResult response = LoginResult.BadCredentials;

            while ((response = mUserLogin.DoLogin()) != LoginResult.LoginOkay)
            {
                switch (response)
                {
                    case LoginResult.NeedCaptcha:
                        CaptchaForm captchaForm = new CaptchaForm(mUserLogin.CaptchaGID);
                        captchaForm.ShowDialog();
                        if (captchaForm.Canceled)
                        {
                            //this.Close();
                            return;
                        }

                        mUserLogin.CaptchaText = captchaForm.CaptchaCode;
                        break;

                    case LoginResult.Need2FA:
                        mUserLogin.TwoFactorCode = androidAccount.GenerateSteamGuardCodeForTime(steamTime);
                        break;

                    case LoginResult.BadRSA:
                        MessageBox.Show("Error logging in: Steam вернул \"BadRSA\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.BadCredentials:
                        MessageBox.Show("Error logging in: Username or password некорректные.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.TooManyFailedLogins:
                        MessageBox.Show("Error logging in: Слишком много неудачных попыток, попробуйте позже.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.GeneralFailure:
                        MessageBox.Show("Error logging in: Steam вернул \"GeneralFailure\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;
                }
            }

            androidAccount.Session = mUserLogin.Session;

            HandleManifest(man, true);
        }

        /// <summary>
        /// Handles logging in after data has been extracted from Android phone
        /// </summary>
        /// <param name="username">Steam username</param>
        /// <param name="password">Steam password</param>
        private async void FinishExtract(string username, string password)
        {
            long steamTime = await TimeAligner.GetSteamTimeAsync();
            Manifest man = Manifest.GetManifest();

            androidAccount.FullyEnrolled = true;

            UserLogin mUserLogin = new UserLogin(username, password);
            LoginResult response = LoginResult.BadCredentials;

            while ((response = mUserLogin.DoLogin()) != LoginResult.LoginOkay)
            {
                switch (response)
                {
                    case LoginResult.NeedEmail:
                        InputForm emailForm = new InputForm("Введите код, отправленный на ваш email:");
                        emailForm.ShowDialog();
                        if (emailForm.Canceled)
                        {
                            //this.Close();
                            return;
                        }

                        mUserLogin.EmailCode = emailForm.txtBox.Text;
                        break;

                    case LoginResult.NeedCaptcha:
                        CaptchaForm captchaForm = new CaptchaForm(mUserLogin.CaptchaGID);
                        captchaForm.ShowDialog();
                        if (captchaForm.Canceled)
                        {
                            //this.Close();
                            return;
                        }

                        mUserLogin.CaptchaText = captchaForm.CaptchaCode;
                        break;

                    case LoginResult.Need2FA:
                        mUserLogin.TwoFactorCode = androidAccount.GenerateSteamGuardCodeForTime(steamTime);
                        break;

                    case LoginResult.BadRSA:
                        MessageBox.Show("Error logging in: Steam вернул \"BadRSA\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.BadCredentials:
                        MessageBox.Show("Error logging in: Username or password некорректные.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.TooManyFailedLogins:
                        MessageBox.Show("Error logging in: Слишком много неудачных попыток, попробуйте позже.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;

                    case LoginResult.GeneralFailure:
                        MessageBox.Show("Error logging in: Steam вернул \"GeneralFailure\".", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //this.Close();
                        return;
                }
            }

            androidAccount.Session = mUserLogin.Session;

            HandleManifest(man);
        }

        private void HandleManifest(Manifest man, bool IsRefreshing = false)
        {
            string passKey = null;
            if (man.Entries.Count == 0)
            {
                passKey = man.PromptSetupPassKey("Введите (Passkey) ключ шифрования. Оставьте пустым или нажмите Отмена, чтобы не шифровать (очень небезопасно).");
            }
            else if (man.Entries.Count > 0 && man.Encrypted)
            {
                bool passKeyValid = false;
                while (!passKeyValid)
                {
                    InputForm passKeyForm = new InputForm("Пожалуйста, введите Ваш текущий ключ шифрования.");
                    passKeyForm.ShowDialog();
                    if (!passKeyForm.Canceled)
                    {
                        passKey = passKeyForm.txtBox.Text;
                        passKeyValid = man.VerifyPasskey(passKey);
                        if (!passKeyValid)
                        {
                            MessageBox.Show("Этот Passkey недействителен. Введите Passkey, который вы использовали для других учетных записей.");
                        }
                    }
                    else
                    {
                        //this.Close();
                        return;
                    }
                }
            }

            man.SaveAccount(androidAccount, passKey != null, passKey);
            if (IsRefreshing)
            {
                MessageBox.Show("Ваш сеанс входа был обновлен.");
            }
            //this.Close();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            if (androidAccount != null && androidAccount.AccountName != null)
            {
                txtUsername.Text = androidAccount.AccountName;
            }
        }

        public enum LoginType
        {
            Initial,
            Android,
            Refresh
        }
    }
}
