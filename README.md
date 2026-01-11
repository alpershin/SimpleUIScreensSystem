# SimpleUIScreensSystem

Простая и эффективная система управления экранами интерфейса для Unity, позволяющая легко переключаться между различными UI-экранами с поддержкой анимаций и асинхронных операций.

## Описание

`SimpleUIScreensSystem` — это легковесный фреймворк для управления UI-экранами в Unity проектах. Система предоставляет:

- **Управление экранами** — регистрация, открытие и закрытие UI-экранов
- **Анимации переходов** — встроенная поддержка fade и scale анимаций
- **Асинхронная работа** — использование UniTask для неблокирующих операций
- **Реактивное программирование** — интеграция с R3 для реактивных событий
- **Гибкие функции интерполяции** — встроенные easing функции (linear, spring, ease-in, ease-out и т.д.)

## Основные компоненты

### UIScreen
Базовый класс для всех UI-экранов. Предоставляет методы для инициализации, открытия, закрытия и управления состоянием экрана.

**Основные методы:**
- `Init()` — инициализация экрана
- `Open()` — открытие экрана
- `Close()` — закрытие экрана
- `OnEnable()`, `OnDisable()` — управление активностью

### UINavigator
Система навигации для управления всеми экранами проекта. Позволяет регистрировать, открывать и закрывать экраны.

**Основные методы:**
- `Add(UIScreen screen)` — регистрация экрана
- `Open(EScreenType screenType)` — открытие экрана по типу
- `Close(EScreenType screenType)` — закрытие экрана
- `CloseAll()` — закрытие всех открытых экранов
- `GetScreen(EScreenType screenType)` — получение экрана

### UIScreenOpenCloseAnimation
Утилита для анимации открытия и закрытия экранов. Поддерживает fade и scale эффекты.

**Основные методы:**
- `FadeIn()` — плавное появление экрана
- `FadeOut()` — плавное исчезновение экрана
- `ScaleAnimation()` — масштабирующая анимация

### EScreenType
Перечисление для определения типов экранов в проекте. Позволяет использовать типобезопасный способ обращения к экранам.

### Easing
Утилита с функциями интерполяции для анимаций.

**Доступные функции:**
- `Linear()` — линейная интерполяция
- `Spring()` — пружинная интерполяция
- `EaseInQuad()`, `EaseOutQuad()` — квадратичная интерполяция
- `EaseInCubic()`, `EaseOutCubic()` — кубическая интерполяция
- и многие другие...

## Установка

Следуйте этапам ниже для установки пакета в ваш Unity проект:

### Этап 1: Установка NuGetPackages

Some компоненты системы требуют поддержку NuGet пакетов. Установите NuGetForUnity:

1. Откройте **Package Manager** в Unity (`Window > TextAsset > Package Manager`)
2. Нажмите кнопку **"+"** и выберите **"Add package from git URL..."**
3. Вставьте ссылку:
   ```
   https://github.com/GlitchEnzo/NuGetForUnity.git
   ```
4. Нажмите **Add**
5. Дождитесь завершения установки

### Этап 2: Установка R3

R3 — это реактивный фреймворк для Unity, необходимый для работы системы.

1. Откройте **Package Manager** (`Window > TextAsset > Package Manager`)
2. Нажмите **"+"** и выберите **"Add package from git URL..."**
3. Вставьте ссылку:
   ```
   https://github.com/Cysharp/R3.git?path=src/R3.Unity
   ```
4. Нажмите **Add**
5. Дождитесь завершения установки

### Этап 3: Установка UniTask

UniTask — это асинхронная библиотека для Unity, используется для неблокирующих операций.

1. Откройте **Package Manager** (`Window > TextAsset > Package Manager`)
2. Нажмите **"+"** и выберите **"Add package from git URL..."**
3. Вставьте ссылку:
   ```
   https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
   ```
4. Нажмите **Add**
5. Дождитесь завершения установки

### Этап 4: Установка SimpleUIScreensSystem

Теперь установите сам пакет.

1. Откройте **Package Manager** (`Window > TextAsset > Package Manager`)
2. Нажмите **"+"** и выберите **"Add package from git URL..."**
3. Вставьте ссылку:
   ```
   https://github.com/alpershin/SimpleUIScreensSystem.git
   ```
4. Нажмите **Add**
5. Дождитесь завершения установки

## Быстрый старт

### 1. Определение типов экранов

Отредактируйте файл `EScreenType.cs` и добавьте типы ваших экранов:

```csharp
public enum EScreenType
{
    None = 0,
    MainMenu = 1,
    Settings = 2,
    Gameplay = 3,
    PauseMenu = 4
}
```

### 2. Создание экрана

Создайте скрипт, наследующийся от `UIScreen`:

```csharp
public class MainMenuScreen : UIScreen
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;

    public override void Init()
    {
        base.Init();
        _type = EScreenType.MainMenu;
        
        startButton.onClick.AddListener(OnStartClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    private void OnStartClicked()
    {
        // Логика при нажатии на кнопку
    }

    private void OnSettingsClicked()
    {
        // Логика для открытия настроек
    }
}
```

### 3. Использование UINavigator

```csharp
public class GameManager : MonoBehaviour
{
    private UINavigator _uiNavigator;

    private void Start()
    {
        _uiNavigator = GetComponent<UINavigator>();
        
        // Регистрация экранов
        _uiNavigator.Add(GetComponent<MainMenuScreen>());
        _uiNavigator.Add(GetComponent<SettingsScreen>());
        
        // Открытие главного меню
        _uiNavigator.Open(EScreenType.MainMenu);
    }
}
```

## Примеры использования

### Открытие экрана с анимацией

```csharp
public class UIScreen : MonoBehaviour
{
    protected async UniTask OpenWithAnimation()
    {
        var animation = GetComponent<UIScreenOpenCloseAnimation>();
        await animation.FadeIn();
    }
}
```

### Закрытие всех экранов

```csharp
_uiNavigator.CloseAll();
```

## Требования

- **Unity:** 2021.3 или выше
- **UniTask:** 2.5.0 или выше
- **R3:** 1.0.0 или выше

## Лицензия

Этот проект лицензирован под MIT License. Подробности в файле [LICENSE](LICENSE).

## Поддержка

Если у вас возникли вопросы или проблемы при использовании пакета, пожалуйста, откройте issue на GitHub репозитории.
