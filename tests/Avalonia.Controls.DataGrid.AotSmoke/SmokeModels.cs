// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridAotSmoke;

public sealed class Department
{
    public Department(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public sealed class Person
{
    public Person(string name, int age, Department department)
    {
        Name = name;
        Age = age;
        Department = department;
    }

    public string Name { get; set; }

    public int Age { get; set; }

    public Department Department { get; set; }
}
