using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace NzbDrone.Automation.Test.PageModel
{
    public class PageBase
    {
        private readonly IWebDriver _driver;

        public PageBase(IWebDriver driver)
        {
            _driver = driver;
        }

        public IWebElement FindByClass(string className, int timeout = 5)
        {
            return Find(By.ClassName(className), timeout);
        }

        public IWebElement Find(By by, int timeout = 5)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));
            return wait.Until(d => d.FindElement(by));
        }

        public void WaitForNoSpinner(int timeout = 30)
        {
            //give the spinner some time to show up.
            Thread.Sleep(200);

            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));
            wait.Until(d =>
            {
                try
                {
                    var element = d.FindElement(By.ClassName("followingBalls"));
                    return !element.Displayed;
                }
                catch (StaleElementReferenceException)
                {
                    return true;
                }
                catch (NoSuchElementException)
                {
                    return true;
                }
            });
        }

        public IWebElement LibraryNavIcon => Find(By.LinkText("Library"));

        public IWebElement CalendarNavIcon => Find(By.LinkText("Calendar"));

        public IWebElement ActivityNavIcon => Find(By.LinkText("Activity"));

        public IWebElement WantedNavIcon => Find(By.LinkText("Wanted"));

        public IWebElement SettingNavIcon => Find(By.LinkText("Settings"));

        public IWebElement SystemNavIcon => Find(By.PartialLinkText("System"));
    }
}
