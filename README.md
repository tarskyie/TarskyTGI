# TarskyTGI

## Overview (English)
TarskyTGI is a .NET 8 application designed to interact with language models using a user-friendly interface. It allows users to load models, generate text, and manage various parameters. 

## Features
- Load and manage language models.
- Generate text based on user input.
- Copy fragments of generated text.
- Navigate between different application pages.
- Customize model parameters like `n_ctx`, `n_predict`, `temperature`, `top_p`, `min_p`, and `typical_p`.

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022
- Windows 10/11

## Installation
1. Clone the repository:
```powershell
git clone https://github.com/tarskyie/TarskyTGI.git
```
2. Open the solution in Visual Studio 2022.
3. Restore NuGet packages:
```powershell
dotnet restore
```

## Usage
1. Run the application from Visual Studio or using the command:
```powershell
dotnet run
```
2. Navigate through the application using the provided navigation menu.
3. Load a model by specifying the model path and clicking the "Start" button.
4. Enter your prompt in the provided text box and click "Send" to generate text.
5. Copy fragments of generated text by selecting the text in the `ChatHistory` list.

## Project Structure
- **TarskyTGI**: Main project folder.
  - **MainWindow.xaml.cs**: Main window logic and navigation.
  - **InstructPage.xaml**: XAML layout for the instruction page.
  - **ChatPage.xaml**: XAML layout for the chat page.
  - **HomePage.xaml**: XAML layout for the home page.
  - **basegenerator.py**: Python script for model interaction.

## Main Components
### MainWindow.xaml.cs
Handles the main window logic, including navigation and process management.

### InstructPage.xaml
Defines the layout and controls for the instruction page, allowing users to set model parameters and generate text.

### ChatPage.xaml
Defines the layout and controls for the chat page, allowing users to interact with the model and copy generated text.

### HomePage.xaml
Defines the layout and controls for the home page, providing options to check and install `llama-cpp-python`.

### basegenerator.py
Python script that loads the language model and generates text based on user input.

## Event Handlers
- **LoadModelButton_Click**: Loads the specified model.
- **ModelBox_TextChanged**: Handles changes in the model path text box.
- **selectModelButton_Click**: Opens a file dialog to select the model file.
- **TextBox_BeforeTextChanging**: Validates input for numeric text boxes.
- **PromptBox_KeyDown**: Handles key down events in the prompt text box.
- **SendFN**: Sends the prompt to the model and displays the generated text.
- **ClearFN**: Clears the chat history.

## Обзор (на русском языке)
TarskyTGI - это приложение для .NET 8, предназначенное для взаимодействия с языковыми моделями с помощью удобного интерфейса. Оно позволяет пользователям загружать модели, генерировать текст и управлять различными параметрами.

## Особенности
- Загрузка и управление языковыми моделями.
- Генерирование текста на основе пользовательского ввода.
- Копирование фрагментов сгенерированного текста.
- Переход между различными страницами приложения.
- Настройка параметров модели, таких как `n_ctx`, `n_predict`, `temperature`, `top_p`, `min_p` и `typical_p`.

## Необходимые условия
- .NET 8 SDK
- Visual Studio 2022
- Windows 10/11

## Установка
1. Клонируйте репозиторий:
```powershell
git clone https://github.com/tarskyie/TarskyTGI.git
```
2. Откройте решение в Visual Studio 2022.
3. Восстановите пакеты NuGet:
```powershell
dotnet restore
```

## Использование
1. Запустите приложение из Visual Studio или с помощью команды:
```powershell
dotnet run
```
2. Перемещайтесь по приложению с помощью навигационного меню.
3. Загрузите модель, указав путь к ней и нажав кнопку «Start».
4. Введите запрос в текстовое поле и нажмите кнопку «Отправить», чтобы сгенерировать текст.
5. Скопируйте фрагменты сгенерированного текста, выбрав текст в списке `ChatHistory`.

## Структура проекта
- **TarskyTGI**: Основная папка проекта.
  - **MainWindow.xaml.cs**: Логика главного окна и навигация.
  - **InstructPage.xaml**: XAML-макет для страницы инструкций.
  - **ChatPage.xaml**: XAML-макет для страницы чата.
  - **HomePage.xaml**: XAML-макет для главной страницы.
  - **basegenerator.py**: Python-скрипт для взаимодействия с моделью.

## Основные компоненты
### MainWindow.xaml.cs
Управляет логикой главного окна, включая навигацию и управление процессами.

### InstructPage.xaml.
Определяет макет и элементы управления для страницы инструкций, позволяя пользователям задавать параметры модели и генерировать текст.

### ChatPage.xaml
Определяет макет и элементы управления для страницы чата, позволяющей пользователям взаимодействовать с моделью и копировать сгенерированный текст.

### HomePage.xaml
Определяет макет и элементы управления для главной страницы, предоставляет опции для проверки и установки `llama-cpp-python`.

### basegenerator.py
Python-скрипт, который загружает языковую модель и генерирует текст на основе пользовательского ввода.

## Обработчики событий.
- **LoadModelButton_Click**: Загружает указанную модель.
- **ModelBox_TextChanged**: Обрабатывает изменения в текстовом поле пути модели.
- **selectModelButton_Click**: Открывает диалог выбора файла модели.
- **TextBox_BeforeTextChanging**: Проверяет вводимые данные для числовых текстовых полей.
- **PromptBox_KeyDown**: Обрабатывает события нажатия клавиш в текстовом поле подсказки.
- **SendFN**: Отправляет подсказку в модель и отображает сгенерированный текст.
- **ClearFN**: Очищает историю чата.
