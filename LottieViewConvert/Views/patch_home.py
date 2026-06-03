import os

path = "/tmp/workspace/SwaggyMacro/LottieViewConvert/LottieViewConvert/Views/HomeView.axaml"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace('RowDefinitions="Auto,Auto,Auto,Auto"', 'RowDefinitions="Auto,Auto,Auto,Auto,Auto"')

searchStr = """                            <StackPanel Grid.Row="3" Grid.Column="1" Margin="0 5 0 0"
                                        Orientation="Horizontal" HorizontalAlignment="Right">"""
replaceStr = """                            <StackPanel Grid.Row="3" Grid.Column="0" Margin="0 5 0 0"
                                        Orientation="Horizontal" Spacing="5" VerticalAlignment="Center">
                                <TextBlock VerticalAlignment="Center" Text="{x:Static lang:Resources.RotateAngle}" />
                                <NumericUpDown Minimum="0" Maximum="360" Increment="90" FormatString="0"
                                               Value="{Binding RotationAngle, Mode=TwoWay}"
                                               Width="100" suki:NumericUpDownExtensions.Unit="°"/>
                            </StackPanel>
                            <StackPanel Grid.Row="3" Grid.Column="1" Margin="20 5 0 0"
                                        Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
                                <CheckBox Content="{x:Static lang:Resources.FlipHorizontal}" IsChecked="{Binding FlipHorizontal}" />
                                <CheckBox Content="{x:Static lang:Resources.FlipVertical}" IsChecked="{Binding FlipVertical}" />
                            </StackPanel>
                            <StackPanel Grid.Row="4" Grid.Column="1" Margin="0 5 0 0"
                                        Orientation="Horizontal" HorizontalAlignment="Right">"""

content = content.replace(searchStr, replaceStr)
content = content.replace('<ProgressBar Grid.Row="4"', '<ProgressBar Grid.Row="5"')

with open(path, "w", encoding="utf-8") as f:
    f.write(content)
