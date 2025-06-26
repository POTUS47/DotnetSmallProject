// FoodSelectorLib.h
#pragma once

using namespace System;
using namespace System::Collections::Generic;

namespace FoodSelectorLib {
    public ref class FoodSelector {
    public:
        static String^ SelectRandomFood(List<String^>^ foodList) {
            if (foodList == nullptr || foodList->Count == 0)
                return String::Empty;

            Random^ random = gcnew Random();
            int index = random->Next(0, foodList->Count);
            return foodList[index];
        }
    };
}