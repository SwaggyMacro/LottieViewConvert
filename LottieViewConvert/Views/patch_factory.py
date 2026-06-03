import os

path = "/tmp/workspace/SwaggyMacro/LottieViewConvert/LottieViewConvert/Views/FactoryView.axaml"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace('RowDefinitions="Auto,Auto,Auto,Auto,Auto"', 'RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto"')

searchStr = """                            <!-- ConcurrentTasks：Slider + NumericUpDown -->
                            <StackPanel Grid.Row="3" Grid.Column="0" Orientation="Horizontal" Spacing="5"
                                        VerticalAlignment="Center">"""
replaceStr = """                            <!-- Rotate and Flip -->
                            <StackPanel Grid.Row="3" Grid.Column="0" Orientation="Horizontal" Spacing="5" VerticalAlignment="Center">
                                <TextBlock VerticalAlignment="Center" Text="{x:Static lang:Resources.RotateAngle}" />
                                <NumericUpDown Minimum="0" Maximum="360" Increment="90" FormatString="0"
                                               Value="{Binding RotationAngle, Mode=TwoWay}"
                                               Width="100" suki:NumericUpDownExtensions.Unit="°"/>
                            </StackPanel>
                            <StackPanel Margin="20 0 0 0" Grid.Row="3" Grid.Column="1" Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
                                <CheckBox Content="{x:Static lang:Resources.FlipHorizontal}" IsChecked="{Binding FlipHorizontal}" />
                                <CheckBox Content="{x:Static lang:Resources.FlipVertical}" IsChecked="{Binding FlipVertical}" />
                            </StackPanel>

                            <!-- ConcurrentTasks：Slider + NumericUpDown -->
                            <StackPanel Grid.Row="4" Grid.Column="0" Orientation="Horizontal" Spacing="5"
                                        VerticalAlignment="Center">"""

content = content.replace(searchStr, replaceStr)

content = content.replace('Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" Orientation="Horizontal"', 'Grid.Row="4" Grid.Column="0" Grid.ColumnSpan="2" Orientation="Horizontal"')
content = content.replace('<StackPanel Grid.Row="4" Grid.Column="0" Grid.ColumnSpan="2" Spacing="5">', '<StackPanel Grid.Row="5" Grid.Column="0" Grid.ColumnSpan="2" Spacing="5">')

with open(path, "w", encoding="utf-8") as f:
    f.write(content)
